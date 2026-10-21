# Quickstart / Doğrulama: NonSecure Banka Çekim Dilimi

Uçtan-uca doğrulama senaryoları. Canlı adımlar **tüm geliştirme bitince, topluca** koşulur (kullanıcı kararı).

## Önkoşul

- Repo kökü; `.NET 10` + Docker (Aspire Postgres + RabbitMQ).
- 3 `SimulatorBankOptions` (banka başına BankCode + sentinel) + `PosAccountOptions` (3 acquirer: bankCode + yetenek + CostPriority) appsettings'te.
- `BinCard` seed JSON (küçük sim BIN seti) embedded; startup'ta yüklenir.
- `StoreCallbackOptions.CallbackSecret` user-secrets'ta (appsettings'te DEĞİL).

## Birim test (domain-TDD, önce)

```bash
dotnet test
```
Beklenen (yeni): `PaymentLinkTests` + `BankChargeRouterTests` yeşil — `RecordAttempt(approved)`→Paid; `RecordAttempt(declined)`→Opened+append; terminal→`TERMINAL_VIOLATION`; Paid sonrası ikinci attempt reddi; router Technical/Config→failover, Issuer/Fraud→dur, ilk-approved→2. banka çağrılmaz, hepsi-technical→son technical; ResponseMapper wire-kod→DeclineClass map. Mevcut 089 testleri bozulmaz (Create imzası değişmez).

## Build + guard

```bash
dotnet build
scripts/check-flow-links.sh
scripts/check-claude-spec-links.sh
```
Beklenen: 0 hata; FLOW.md yeni edge-anchor tipleri (`PaymentLink.RecordAttempt`, `BankChargeRouter.RouteAsync`, `StoreResultDelivery.Deliver`, `SimulatorBankAdapter`) kodda var.

## Canlı E2E (Aspire)

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

1. **Onay akışı (US1)**: `POST /payment-links` (payment.write) → link al → `GET /payment-links/{openToken}` güven bağlamı + kart formu → onay-kartıyla `POST .../pay` → yanıt `Paid` → DB'de link Paid + 1 başarılı attempt → store webhook alındı (X-Signature CallbackSecret ile doğrulanır) → RabbitMQ'da `PaymentCompletedEvent`.
2. **Issuer reddi (US2)**: "yetersiz bakiye" sentinel kartı → `/pay` → link `Opened` kalır + 1 başarısız attempt (IssuerDecline) → yanıt `CHARGE_DECLINED` → `PaymentFailedEvent` → aynı link başka kartla tekrar denenebilir.
3. **Failover kararı (US3)**: iki banka tanımlı (test config) + ilk technical → ikinci denenir/onaylar; issuer/fraud → ikinci denenmez. (Birim testle de kanıtlı; canlıda iki simüle banka opsiyonel.)
4. **089 bekleyenler**: T026 canlı Aspire + F4 partial-index predicate teyidi bu turda birlikte.

## İlke VI denetimi (zorunlu)

- PAN/CVV: DB dokümanında, log'da, RabbitMQ/dayanıklı kuyrukta, HTTP yanıtında → **görünmemeli** (grep + inceleme).
- `CallbackSecret`: log/trace/webhook gövdesi/dönüş DTO'sunda → **görünmemeli**.
- Command publish edilmiyor (`InvokeAsync` only) → PAN outbox'a yazılmıyor.
