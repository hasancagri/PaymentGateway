# API Contract: Commission Policy (024)

BC: `Commission.Api`. Taban: `api/v{version:apiVersion}/commission-policies` (v1.0). Tüm uçlar
Minimal API + `CommissionPolicyEndpointExtension` ile map'lenir; her uç yetkisini AÇIKÇA beyan eder
(anayasa V). Sonuç zarfı: başarı `FeatureObjectResultModel<T>.Data` (200), hata `400` +
`FeatureObjectResultModel` (Türkçe `MessageText`). Token yoksa `401`, yetki uyuşmazlığı `403`.

Yetki matrisi:

| Uç | Metot | Yol | Scope | Policy |
|----|-------|-----|-------|--------|
| Create | POST | `/` | `commission.write` | `AdminPlaneOnly` |
| Update margin | PUT | `/{merchantId}/margin` | `commission.write` | `AdminPlaneOnly` |
| Change status | PUT | `/{merchantId}/status` | `commission.write` | `AdminPlaneOnly` |
| Calculate | POST | `/effective-commission` | `commission.read` | `AdminPlaneOnly` |
| Get (merchant self) | GET | `/{merchantId}` | `commission.read` | `MerchantScoped` |
| List (admin) | GET | `/` | `commission.read` | `AdminPlaneOnly` |

> `MerchantScoped`: route `{merchantId}` token `merchant_id` claim'iyle eşleşmeli (yoksa fail-closed).
> `AdminPlaneOnly`: `merchant_id` claim'li token GİREMEZ (403).

---

## 1. Create Commission Policy — POST `/`

Yönetici bir merchant için gateway marj politikası oluşturur (FR-001, US1). Tekil-aktif (FR-005).

Request:
```json
{
  "merchantId": "9f1c2b3a-...-guid",
  "ratePercent": 0.015,
  "fixedFee": 0.50
}
```

200:
```json
{ "policyId": "d4e5f6...-guid", "merchantId": "9f1c...", "ratePercent": 0.015, "fixedFee": 0.50, "status": "Active" }
```

400 durumları: geçersiz marj (negatif / cap aşımı, FR-004); boş merchantId; **aynı merchant'ta
aktif politika zaten var** (`COMMISSION_POLICY_ALREADY_EXISTS`, FR-005).

---

## 2. Update Margin — PUT `/{merchantId}/margin`

Mevcut politikanın marjını günceller (FR-002). İleriye dönük — geçmiş hesaplar etkilenmez.

Request:
```json
{ "ratePercent": 0.02, "fixedFee": 1.00 }
```

200: güncel politika (`ratePercent`, `fixedFee`, `status`). 400: geçersiz marj; merchant'ın
politikası yok (`COMMISSION_POLICY_NOT_FOUND`).

---

## 3. Change Status — PUT `/{merchantId}/status`

Politikayı aktif/pasif yapar (FR-003). Aynı statü → idempotent no-op (200, değişiklik yok).

Request:
```json
{ "status": "Passive" }
```

200: `{ "merchantId": "...", "status": "Passive" }`. 400: geçersiz statü değeri; politika yok.

---

## 4. Calculate Effective Commission — POST `/effective-commission`

Verili işlem bağlamı için efektif komisyon + net hakediş (FR-006/FR-007, US2). iyzico maliyeti
GİRDİ (işlem-sonrası rapordan, string alanlar — R1/R9). Durum değiştirmez (Query).

Request:
```json
{
  "merchantId": "9f1c...",
  "paidPrice": 1000.00,
  "iyzicoCommission": "18.50",
  "iyzicoFee": "0.25",
  "installment": 3
}
```

200:
```json
{
  "merchantId": "9f1c...",
  "paidPrice": 1000.00,
  "installment": 3,
  "iyzicoCost": 18.75,
  "gatewayMargin": 15.50,
  "totalEffectiveCommission": 34.25,
  "netPayout": 965.75
}
```
(örnek: margin = 1000·0.015 + 0.50 = 15.50; iyzicoCost = 18.50 + 0.25 = 18.75)

400 durumları: merchant'ın **aktif politikası yok** (`COMMISSION_POLICY_NOT_FOUND`, FR-008 — sessiz
0 YOK); politika Passive (`COMMISSION_POLICY_NOT_ACTIVE`); iyzico maliyeti eksik/ayrıştırılamaz
(`COMMON_MESSAGE_INVALID_VALUE`, FR-012); efektif komisyon > paidPrice
(`COMMISSION_EXCEEDS_PAID_PRICE`, FR-009 — negatif hakediş YOK).

---

## 5. Get My Commission Policy — GET `/{merchantId}`

Merchant kendi politikasını/efektif oranını görür (FR-010, US3). `MerchantScoped` — yalnız kendi
`{merchantId}`. Başka merchant → 403 (fail-closed).

200:
```json
{ "merchantId": "9f1c...", "ratePercent": 0.015, "fixedFee": 0.50, "status": "Active" }
```
404/400: politika yok (`COMMISSION_POLICY_NOT_FOUND`).

---

## 6. List Commission Policies — GET `/`

Admin genel bakış (opsiyonel `?merchantId=` / `?status=` filtre). `AdminPlaneOnly`.

200: `[ { policyId, merchantId, ratePercent, fixedFee, status }, ... ]`