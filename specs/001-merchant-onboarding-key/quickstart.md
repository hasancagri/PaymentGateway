# Quickstart / Doğrulama: Merchant.Api + Commission.Api

Uçtan uca doğrulama senaryosu. Amaç: iki BC'nin ayrı ayrı çalıştığını ve invariant'ların
zorlandığını kanıtlamak. Detay tip/alan için [data-model.md](./data-model.md),
uç şekilleri için [contracts/](./contracts/).

## Önkoşul

- .NET 10 SDK, Docker (Aspire Postgres + RabbitMQ container'ları).
- Sistem **her zaman AppHost'tan** kalkar (CLAUDE.md). Tek servis izole çalıştırılmaz.
- AppHost `merchantDb` + `commissionDb` + `merchant-api` + `commission-api` içerir (bu dilimde eklenir).

## Kaldırma

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

Aspire dashboard'dan `merchant-api` ve `commission-api` URL'lerini al. Aşağıda `$M` =
merchant-api kökü, `$C` = commission-api kökü.

---

## Senaryo 1 — Merchant oluştur (US1 registry ayağı)

```bash
curl -X POST $M/api/v1/merchants -H 'Content-Type: application/json' -d '{
  "name":"Acme Ltd","email":"ops@acme.com","phone":"+902121234567",
  "countryCode":"TR","cityCode":"34","mcc":"5411",
  "webhookUrl":"https://acme.com/webhooks/payments" }'
```
**Beklenen:** `200` + `{ "id": "<MERCHANT_ID>" }`. `GET $M/api/v1/merchants/<id>` → `status:"Active"`,
`mccName:"Grocery Stores"` (lookup'tan çözülür).

**Doğrulama (US1-AC3):** geçersiz gönder → red.
```bash
curl -X POST $M/api/v1/merchants -H 'Content-Type: application/json' -d '{
  "name":"X","email":"not-an-email","phone":"1","countryCode":"TR","cityCode":"34",
  "mcc":"12","webhookUrl":"acme.com" }'
```
**Beklenen:** `400` + `Messages` içinde `Email` (INVALID_FORMAT), `Mcc` (INVALID_FORMAT/`^\d{4}$`),
`WebhookUrl` (INVALID_FORMAT). Kayıt oluşmaz.

---

## Senaryo 2 — Banka komisyonu tanımla (invariant referansı)

```bash
curl -X POST $C/api/v1/bank-commissions -H 'Content-Type: application/json' -d '{
  "bankCode":"0062",
  "criteria":{"cardBrand":"VISA","cardType":"CREDIT","transactionRegion":"DOMESTIC","installmentCount":6},
  "rate":1.75 }'
```
**Beklenen:** `200` + `{ "id": "<BANK_COMM_ID>" }`. Aynı `(bankCode, criteria)` tekrar → `400`
RECORD_DUPLICATE.

---

## Senaryo 3 — Merchant komisyonu: invariant (US2, SC-003)

**3a — geçerli (banka oranından yüksek):**
```bash
curl -X POST $C/api/v1/merchant-commissions -H 'Content-Type: application/json' -d '{
  "merchantId":"<MERCHANT_ID>","bankCommissionId":"<BANK_COMM_ID>","rate":2.40 }'
```
**Beklenen:** `200` (2.40 > 1.75).

**3b — eşit/altı (red):**
```bash
curl -X POST $C/api/v1/merchant-commissions -H 'Content-Type: application/json' -d '{
  "merchantId":"<MERCHANT_ID>","bankCommissionId":"<BANK_COMM_ID>","rate":1.75 }'
```
**Beklenen:** `400` + `MERCHANT_RATE_MUST_EXCEED_BANK_RATE`. (Eşit bile reddedilir — kesin büyük.)
`1.50` de aynı red. **SC-003: %100 red.**

**3c — tekrar giriş = güncelle (Edge Case):** 3a'yı `rate:2.60` ile tekrar POST → aynı
`(MerchantId, BankCommissionId)` güncellenir (yeni kayıt açılmaz).

---

## Senaryo 4 — İzolasyon: başka merchant sızmaz (SC-004)

İkinci bir merchant + komisyonu oluştur, sonra:
```bash
curl "$C/api/v1/merchant-commissions?merchantId=<MERCHANT_ID>"
```
**Beklenen:** Yalnız `<MERCHANT_ID>` kayıtları; ikinci merchant'ın kaydı **0**. (Düz `MerchantId`
filtresi — tenant enforcement sonraki dilim.)

---

## Domain birim testleri (host'suz — anayasa test kuralı)

`tests/Commission.Domain.Tests` (xUnit), saf domain — HTTP/DB yok:

- `MerchantTests`: geçerli `Create` → Ok; boş email/isim → Error; MCC `^\d{4}$` format; webhook URL;
  `Suspend()`/`Deactivate()` durum geçişleri.
- `MerchantCommissionTests`: `rate > bankRate` → Ok; `rate == bankRate` → Error;
  `rate < bankRate` → Error; `UpdateRate` aynı invariant.
- `BankCommissionTests`: bankCode 4 hane, rate ≥ 0.

```bash
dotnet test tests/Commission.Domain.Tests
```

---

## Başarı ölçütü eşlemesi

| Senaryo | Spec |
|---|---|
| 1 (oluştur + geçersiz red) | US1 registry, FR-003, US1-AC3 |
| 2 (banka oranı) | invariant referansı |
| 3b (eşit/altı red) | FR-008, SC-003 |
| 3c (tekrar = güncelle) | Edge Case, FR-009 |
| 4 (izolasyon) | FR-010, SC-004 |

> **Kapsam dışı (sonraki dilim):** `umk_` key üretimi/gösterimi, seed admin girişi, scope
> enforcement, Marten conjoined tenant. Bu quickstart yalnız iki registry/komisyon BC'sini doğrular.