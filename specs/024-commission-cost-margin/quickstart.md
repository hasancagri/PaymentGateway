# Quickstart: Commission Cost + Margin (024)

Feature'ın uçtan uca çalıştığını kanıtlayan elle doğrulama senaryoları. Detay:
[contracts/commission-policy.http.md](./contracts/commission-policy.http.md),
[data-model.md](./data-model.md).

## Ön koşullar

- Sistem Aspire ile ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
  (Postgres + RabbitMQ + Identity.Server + Commission.Api). Tek servisi izole çalıştırma
  desteklenmez.
- Token'lar Identity.Server'dan (`https://localhost:5101/connect/token`, client_credentials):
  - **Admin token**: `admin-ui` istemcisi, scope `commission.read commission.write` (claim'siz →
    `AdminPlaneOnly` geçer).
  - **Merchant token**: `client_id=<merchantId>`, `client_secret=<MerchantKey>` (023'te oluşan bir
    Active merchant); token `merchant_id` claim'i taşır (→ `MerchantScoped`).
- Bir Active merchant kaydı (023 `POST /api/v1/merchants`) ve `MerchantId`'si hazır.

## Birim testleri (saf domain — DB/ağ yok)

```bash
dotnet test tests/Commission.Api.Tests
```

Beklenen: yeşil. Kapsar — `MarginRule.Create` (negatif/cap/geçerli), `CommissionPolicy` (Create,
UpdateMargin, statü idempotent no-op), `CalculateEffectiveCommission` (aritmetik+yuvarlama SC-002,
not-active, ayrıştırılamaz maliyet, efektif>PaidPrice reddi SC-005).

## Senaryo S1 — Politika oluştur (US1 / FR-001, FR-004, FR-005)

1. Admin token ile `POST /api/v1/commission-policies` `{merchantId, ratePercent:0.015, fixedFee:0.50}`.
   → **200**, `status:"Active"`.
2. Aynı merchant'a tekrar `POST` → **400** `COMMISSION_POLICY_ALREADY_EXISTS` (tekil-aktif, FR-005).
3. `ratePercent:-0.1` veya `0.99` (cap %20 aşımı) ile `POST` → **400** geçersiz marj (FR-004).

**Kanıt (SC-001)**: politika tek işlemde oluşur, doğrulanmış marjla döner.

## Senaryo S2 — Efektif komisyon hesabı (US2 / FR-006, FR-007, SC-002)

1. Admin token ile `POST /effective-commission`
   `{merchantId, paidPrice:1000.00, iyzicoCommission:"18.50", iyzicoFee:"0.25", installment:3}`.
   → **200**: `gatewayMargin:15.50`, `iyzicoCost:18.75`, `totalEffectiveCommission:34.25`,
   `netPayout:965.75`.
2. Elle aritmetik: `1000·0.015 + 0.50 = 15.50`; `18.50 + 0.25 = 18.75`; toplam `34.25`;
   net `965.75`. → API sonucu %100 eşit (**SC-002**, deterministik yuvarlama).

## Senaryo S3 — Politika yok / pasif (FR-008, FR-003, SC-003)

1. Politikası OLMAYAN bir merchantId ile `POST /effective-commission` → **400**
   `COMMISSION_POLICY_NOT_FOUND` (sessiz 0 YOK — **SC-003**).
2. `PUT /{merchantId}/status` `{status:"Passive"}` → **200**. Ardından `POST /effective-commission`
   → **400** `COMMISSION_POLICY_NOT_ACTIVE` (FR-003).
3. `PUT /{merchantId}/status` `{status:"Passive"}` tekrar → **200** idempotent no-op.

## Senaryo S4 — Tutarsızlık reddi (FR-009, SC-005)

1. Marjı yükselt: `PUT /{merchantId}/margin` `{ratePercent:0.02, fixedFee:1.00}` → **200**.
2. `POST /effective-commission` `{paidPrice:10.00, iyzicoCommission:"9.50", iyzicoFee:"1.00", installment:1}`
   → efektif = 10.50 + 1.20 = 11.70 > 10.00 → **400** `COMMISSION_EXCEEDS_PAID_PRICE`
   (negatif hakediş YOK — **SC-005**).
3. `iyzicoCommission:"abc"` (ayrıştırılamaz) ile → **400** `COMMON_MESSAGE_INVALID_VALUE` (FR-012).

## Senaryo S5 — Merchant self-görüntüleme + izolasyon (US3 / FR-010, SC-004)

1. **Merchant token** (kendi `merchant_id`) ile `GET /commission-policies/{kendiMerchantId}` →
   **200**, marj + statü döner.
2. Merchant token ile `GET /commission-policies/{başkaMerchantId}` → **403** fail-closed
   (**SC-004**).
3. Admin token ile `GET /commission-policies/{başkaMerchantId}` → **403** `AdminPlaneOnly` GET'i
   `{merchantId}` route'ta `MerchantScoped`; admin liste için `GET /commission-policies` kullanır →
   **200** liste.

## Başarı ölçütleri eşlemesi

| Senaryo | Doğrular |
|---------|----------|
| S1 | SC-001, FR-001/004/005 |
| S2 | SC-002, FR-006/007 |
| S3 | SC-003, FR-003/008 |
| S4 | SC-005, FR-009/012 |
| S5 | SC-004, FR-010 |

> Not (anayasa): quickstart elle canlı doğrulama = spec kanıtı. Otomatik regression (Playwright
> E2E) bu iterasyonda kapsam dışı; saf domain birim testleri (`dotnet test`) determinizmi kanıtlar.