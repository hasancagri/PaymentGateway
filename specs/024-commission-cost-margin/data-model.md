# Phase 1 Data Model: Commission Cost + Margin

Kaynak: [spec.md](./spec.md) Key Entities + FR-001..FR-013 + [research.md](./research.md).
Kalıcılık: Marten document store, `commission` şeması. Tek aggregate: `CommissionPolicy`.

## Aggregate: CommissionPolicy

`Domains/CommissionPolicies/CommissionPolicy.cs` — `: AggregateRoot` (tek kök, klasör kuralı).

| Alan | Tip | Kural |
|------|-----|-------|
| `Id` | `Guid` | Marten kimliği (AggregateRoot'tan). |
| `MerchantId` | `Guid` | Dış referans (Merchant BC). Zorunlu, boş Guid reddedilir. Cross-BC doğrulanmaz (R7). |
| `Margin` | `MarginRule` (VO) | Gateway marjı — oran + sabit ücret. |
| `Status` | `CommissionPolicyStatus` | `Active` / `Passive`. Create → `Active`. |
| `CreatedAt` | `DateTime` (UTC) | Fabrikada set. |
| `UpdatedAt` | `DateTime` (UTC) | Her mutasyonda güncellenir. |

### Fabrika ve davranışlar (hepsi handler'dan çağrılır — 015; `ResultDomain` sarılı — 014)

| Metot | İmza | Kural / Handler |
|-------|------|-----------------|
| `Create` | `(Guid merchantId, decimal ratePercent, decimal fixedFee) : ResultDomain<CommissionPolicy>` | `merchantId` boş-Guid reddi; `MarginRule.Create` çağrılır (inline değil — VO fabrikası, muaf), hata yayılır. Geçerse `Active` doğar, `CreatedAt=UpdatedAt=now`. Handler: `CreateCommissionPolicyCommandHandler`. Tekil-aktif kuralı (FR-005) handler-sorgusunda (R6). |
| `UpdateMargin` | `(decimal ratePercent, decimal fixedFee) : ResultDomain` | Yeni `MarginRule.Create` doğrular; geçerse `Margin` değişir, `UpdatedAt=now`. Handler: `UpdateCommissionPolicyMarginCommandHandler`. |
| `Activate` | `() : ResultDomain` | `Passive→Active`. Zaten `Active` ise idempotent no-op (başarı, değişiklik yok). Handler: `ChangeCommissionPolicyStatusCommandHandler`. |
| `Deactivate` | `() : ResultDomain` | `Active→Passive`. Zaten `Passive` ise idempotent no-op. Handler: `ChangeCommissionPolicyStatusCommandHandler`. |
| `CalculateEffectiveCommission` | `(decimal paidPrice, string iyzicoCommission, string iyzicoFee, int installment) : ResultDomain<EffectiveCommission>` | Bkz. aşağıdaki algoritma. Handler: `CalculateEffectiveCommissionQueryHandler`. |

> Not: `Create` içinde `MarginRule.Create` çağrısı 015 "metot→metot yasağı"na TAKILMAZ — çağrılan
> bir **VO fabrikası** (VO muaf). Statü metotları ayrı (`Activate`/`Deactivate`) — handler enum
> girdisine göre birini çağırır; aggregate-içi metot→metot çağrısı yok.

### Statü makinesi (CommissionPolicyStatus — düz enum)

```
Active  --Deactivate-->  Passive
Passive --Activate---->  Active
(aynı statü → idempotent no-op, başarı döner)
```

Passive politika hesaplamada yok sayılır (FR-003): hesaplama isteği "aktif değil" hatası döner.

## Value Object: MarginRule

`Domains/CommissionPolicies/ValueObjects/MarginRule.cs` — private ctor + statik `Create`
(AggregateRoot değil). VO helper serbest (015 muaf).

| Alan | Tip | Kural |
|------|-----|-------|
| `RatePercent` | `decimal` | 0 ≤ oran ≤ `MaxRatePercent` (0.20). Ondalık kesir (0.015 = %1.5). |
| `FixedFee` | `decimal` | 0 ≤ ücret ≤ `MaxFixedFee` (100 TL). |

Sabitler (VO-içi domain sabiti): `MaxRatePercent = 0.20m`, `MaxFixedFee = 100m`.

`Create(decimal ratePercent, decimal fixedFee) : ResultDomain<MarginRule>`:
- `ratePercent < 0` veya `> MaxRatePercent` → `Error` (Property `RatePercent`).
- `fixedFee < 0` veya `> MaxFixedFee` → `Error` (Property `FixedFee`).
- Geçerse `Ok(new MarginRule(...))`.

## Value Object / Read Model: EffectiveCommission

`Domains/CommissionPolicies/ValueObjects/EffectiveCommission.cs` — hesap-sonucu dökümü (kalıcı
değil; `CalculateEffectiveCommission` döner).

| Alan | Tip | Anlam |
|------|-----|-------|
| `PaidPrice` | `decimal` | İşlemin ödenen tutarı (girdi). |
| `Installment` | `int` | Taksit sayısı (girdi, bilgi amaçlı). |
| `IyzicoCost` | `decimal` | `IyzicoCommission + IyzicoFee` (ayrıştırılmış toplam). |
| `GatewayMargin` | `decimal` | `Round(PaidPrice*RatePercent + FixedFee, 2)`. |
| `TotalEffectiveCommission` | `decimal` | `IyzicoCost + GatewayMargin`. |
| `NetPayout` | `decimal` | `PaidPrice − TotalEffectiveCommission`. |

### Hesaplama algoritması (CalculateEffectiveCommission)

```
1. Status != Active            → Error(COMMISSION_POLICY_NOT_ACTIVE)
2. paidPrice <= 0              → Error(INVALID_VALUE, PaidPrice)
3. iyzicoCommission/iyzicoFee decimal.TryParse (InvariantCulture) BAŞARISIZ
                              → Error(INVALID_VALUE, IyzicoCommission/IyzicoFee)   # FR-012, sessiz 0 yok
4. iyzicoCost   = parse(iyzicoCommission) + parse(iyzicoFee)
5. margin       = Round(paidPrice * Margin.RatePercent + Margin.FixedFee, 2, AwayFromZero)   # R3
6. effective    = iyzicoCost + margin
7. effective > paidPrice       → Error(COMMISSION_EXCEEDS_PAID_PRICE)              # FR-009, negatif hakediş yok
8. netPayout    = paidPrice - effective
9. Ok(new EffectiveCommission{ paidPrice, installment, iyzicoCost, margin, effective, netPayout })
```

## İlişkiler ve sınırlar

- `CommissionPolicy` 1—1 `MarginRule` (gömülü VO, ayrı document değil).
- `CommissionPolicy` N—1 `MerchantId` — kuralca **1 aktif** (FR-005, R6 handler-enforced).
- `EffectiveCommission` kalıcı değil; işlem-başına türetilir (spec: geçmiş hesaplar yeniden
  fiyatlanmaz — marj güncellemesi ileriye dönük, R1/Assumptions).
- iyzico `TransactionReports`/`Payouts`/`Provider` tipleri UYUR — bu modelle ilişkisi yok; maliyet
  yalnız string girdi olarak `CalculateEffectiveCommission`'a gelir (BC sınırı korunur).

## Resource sabitleri (yeni — `MessageItem.Code`)

Anayasa IV: `Code` serbest metin değil resource sabiti. Gerekecekler (Common resource'a eklenir
ya da mevcut `CommonResourceConstants` kullanılır):

- `COMMON_MESSAGE_VALUE_IS_REQUIRED` (mevcut) — boş MerchantId vb.
- `COMMON_MESSAGE_INVALID_VALUE` (mevcut) — ayrıştırılamaz maliyet, geçersiz PaidPrice, cap aşımı.
- `COMMISSION_POLICY_NOT_ACTIVE` (yeni) — pasif/olmayan politikada hesaplama.
- `COMMISSION_POLICY_ALREADY_EXISTS` (yeni) — tekil-aktif ihlali (FR-005).
- `COMMISSION_EXCEEDS_PAID_PRICE` (yeni) — efektif > PaidPrice (FR-009).
- `COMMISSION_POLICY_NOT_FOUND` (yeni) — merchant'ın aktif politikası yok (FR-008, hesaplama/GET).

> Yeni sabitlerin tam yeri (`Common` resource dosyası) tasks aşamasında netleşir; mevcut örüntü
> `CommonResourceConstants`.