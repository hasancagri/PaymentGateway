# Contract: Commission.Api

Minimal API + Wolverine. Sürüm `v1`. Bu dilimde **korumasız**. Sonuç `FeatureObjectResultModel<T>`.
Tek servis, iki aggregate: `BankCommission` (maliyet, global) + `MerchantCommission` (gelir).
Invariant `merchantRate > bankRate` **in-process**.

`Criteria` her istekte kod olarak taşınır:
```json
"criteria": { "cardBrand": "VISA", "cardType": "CREDIT", "transactionRegion": "DOMESTIC", "installmentCount": 6 }
```

---

## BankCommission (gateway maliyeti — global)

### POST /api/v1/bank-commissions — CreateBankCommission

**Request**
```json
{
  "bankCode": "0062",
  "criteria": { "cardBrand": "VISA", "cardType": "CREDIT", "transactionRegion": "DOMESTIC", "installmentCount": 6 },
  "rate": 1.75
}
```
**200** → `{ "id": "bank-comm-guid" }`
**400** → bankCode 4 hane değil / rate < 0 (INVALID_FORMAT / INVALID_RANGE) veya
`(BankCode, Criteria)` duplicate (RECORD_DUPLICATE).

### GET /api/v1/bank-commissions — GetBankCommissions

Kombinasyon matrisi (admin UI referansı). Filtre opsiyonel: `?bankCode=0062`.
**200**
```json
[ { "id": "bank-comm-guid", "bankCode": "0062",
    "criteria": { "cardBrand": "VISA", "cardType": "CREDIT", "transactionRegion": "DOMESTIC", "installmentCount": 6 },
    "rate": 1.75 } ]
```

---

## MerchantCommission (gateway geliri — MerchantId filtreli)

### POST /api/v1/merchant-commissions — CreateMerchantCommission

Belirli bir `BankCommission`'a bağlanır; invariant onun oranına karşı. `(MerchantId,
BankCommissionId)` zaten varsa → **güncelle** (upsert, spec Edge Case).

**Request**
```json
{ "merchantId": "8f3c...", "bankCommissionId": "bank-comm-guid", "rate": 2.40 }
```
**200** → `{ "id": "merch-comm-guid" }`
**400** → `rate <= bankRate` (MERCHANT_RATE_MUST_EXCEED_BANK_RATE) / merchantId boş /
bankCommissionId bulunamadı (RECORD_NOT_FOUND).

> Not: `merchantId` yalnız `Guid`; Merchant.Api'ye doğrulama çağrısı YOK (Karar 7).

### PUT /api/v1/merchant-commissions/{id} — UpdateMerchantCommission

**Request** `{ "rate": 2.60 }` → aynı invariant.
**200** / **400** (invariant) / **404** (yok).

### GET /api/v1/merchant-commissions?merchantId={guid} — GetMerchantCommissions

Yalnız o merchant'ın kayıtları — düz `Where(MerchantId == ...)` (tenant ertelendi, Karar 5).
Başka merchant sızmaz (SC-004).
**200**
```json
[ { "id": "merch-comm-guid", "merchantId": "8f3c...", "bankCommissionId": "bank-comm-guid",
    "bankCode": "0062",
    "criteria": { "cardBrand": "VISA", "cardType": "CREDIT", "transactionRegion": "DOMESTIC", "installmentCount": 6 },
    "rate": 2.40 } ]
```

---

## (Ertelendi — bu dilim yok)

- Marten `MultiTenanted` / `ForTenant` → sonraki dilim (düz filtre şimdilik).
- Yetki (`commissions.manage` scope) → Identity dilimi.
- BankCommission ↔ PosAccount tek-kaynak uzlaştırma → sonraki dilim (Obsidian todo).