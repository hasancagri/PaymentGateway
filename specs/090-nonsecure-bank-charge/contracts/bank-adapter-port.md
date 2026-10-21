# Contract: Banka Çekim Portu (İlke VII)

Ortak port; her banka kendi adapter'ıyla gerçekler. Wire tipleri adapter'ın İÇİNDE private nested (domain sınırını geçmez). Adapter wire cevabını normalize `BankChargeOutcome`'a çevirir (anti-corruption).

## Port

```csharp
public interface IBankPaymentAdapter
{
    string BankCode { get; }
    Task<ResultDomain<BankChargeOutcome>> ChargeAsync(BankChargeRequest request, CancellationToken ct);
}
```

- `ChargeAsync` NonSecure sale (bu versiyon). İleride port genişler: iptal/iade/durum-sorgu/BIN (seam, bu versiyonda YOK).
- Dönüş: `Ok(BankChargeOutcome)` — onay VEYA red (outcome taşır). `Error` yalnız gerçek programlama hatası (yönlendirilemez).
- `request.Card` transient (PAN/CVV); adapter wire'a çevirir, saklamaz/loglamaz.

## Normalize cevap (BankChargeOutcome)

| Alan | Not |
|---|---|
| `IsApproved` | onay/red |
| `DeclineClass?` | red sınıfı (Issuer/Technical/Config/Fraud); onayda null |
| `ProviderRef?` | opak banka referansı |
| `AuthCode?` | onay kodu |
| `RawCode` | banka ham kodu (denetim; pos-master `proc_return_code`/`error_code` aynası) |

## SimulatorBankAdapter (3 canlı örnek)

- Tek adapter sınıfı, config'le 3 kez parametrize (banka başına `SimulatorBankOptions` + farklı `BankCode`).
- `BankCode` = `SimulatorBankOptions.BankCode` (literal yok).
- Private nested wire: `SimulatorChargeRequestWire` / `SimulatorChargeResponseWire`.
- Private `ResponseMapper`: wire status/kod → `BankChargeOutcome` (NonSecure için `MapNonSecure`; 3DS `Map3DS` seam yorumlu, yazılmaz).
- Test-veri sürücüsü (`SimulatorBankOptions` sentinel'leri): kart/tutar → canned sonuç:
  - onay kartı → `Approved`
  - "yetersiz bakiye" sentinel → `Declined(IssuerDecline, "51")`
  - "teknik hata" sentinel → `Declined(TechnicalFailure, ...)`
  - "fraud" sentinel → `Declined(FraudDecline, ...)`
- Hesap/bakiye modeli YOK.

## Router sözleşmesi (BankChargeRouter) — 2-aşama + canlı failover

Girdi: `BankChargeRequest` + `PosAccountOptions[]` + BinCard lookup.

**Aşama 1 — aday kur (İlke VII):**
1. `BinCard.ResolveByPan(pan)` → issuerBankCode (ya da null = bilinmeyen BIN).
2. Yetenek eleme: `PosAccountOptions` içinde bu kart tipini/işlemi desteklemeyen acquirer çıkar.
3. Sıralama: on-us (issuer == PosAccount.BankCode) en öne → kalanlar `CostPriority` artan. Bilinmeyen BIN → on-us atla, yalnız maliyet (fail-open).

**Aşama 2 — sıralı dene (failover):**

| İlk/ara sonuç | Aksiyon |
|---|---|
| Approved | DUR, onay döndür (kalan aday çağrılmaz) |
| Declined(IssuerDecline) | DUR, red döndür (failover YANLIŞ — aynı issuer) |
| Declined(FraudDecline) | DUR, red döndür (asla retry) |
| Declined(TechnicalFailure/ConfigError) | SIRADAKİ adaya geç |
| Aday listesi tükendi (hepsi technical/config) | son technical outcome döndür |

**Çift-çekim seam (belgeli, ARTIK CANLI)**: 3 banka canlı → TechnicalFailure'da failover FİİLEN koşar; 1. banka çekmiş olabilir, durum-sorgu YOK → çift-çekim penceresi açık. Simülatörde bakiye YOK → şu an zararsız. GERÇEK banka öncesi durum-sorgu ZORUNLU (sonraki versiyon). Health/circuit-breaker = `BankRoutingOptions` seam (TODO).
