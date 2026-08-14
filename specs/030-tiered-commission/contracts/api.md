# Contract: Commission.Api Kademeli Tarife Uçları (030)

Rotalar, policy'ler, tekil-aktif kuralı 024'tekiyle AYNI; yalnız gövde/yanıt şekilleri değişir.
`TierDto`: `{ "fromAmount": 0, "ratePercent": 0.025, "fixedFee": 1 }` (fromAmount TL, dahil;
ratePercent kesir).

## `POST /api/v1/commission-policies` (commission.write + AdminPlaneOnly)

```json
{ "merchantId": "guid", "tiers": [
  { "fromAmount": 0,     "ratePercent": 0.025, "fixedFee": 1 },
  { "fromAmount": 1000,  "ratePercent": 0.02,  "fixedFee": 1 },
  { "fromAmount": 10000, "ratePercent": 0.018, "fixedFee": 0 } ] }
```

Başarı: `{ policyId, merchantId, tiers: [...], status: "Active" }`.
Hata: FR-002 ihlalinde `Property = Tiers[i].<Alan>` işaretli `INVALID_VALUE`; aktif politika
varken `RECORD_DUPLICATE` (aynen).

## `PUT /api/v1/commission-policies/{merchantId}/margin`

Gövde: `{ "merchantId": "guid", "tiers": [...] }` — tablo bütünüyle değişir. Başarı/hata Create ile aynı şekil.

## `GET /api/v1/commission-policies` (liste) ve `GET /{merchantId}` (MerchantScoped self)

Item/yanıt: `{ policyId, merchantId, tiers: [...], status, createdTime }` — düz
`ratePercent/fixedFee` alanları KALKAR.

## `POST /api/v1/commission-policies/effective-commission` — DEĞİŞMEZ

Girdi/çıktı aynı; marj satırı içeride tutarın düştüğü kademeden hesaplanır. Spec örnekleri
(500 → 13,50; 1.000 → 21,00; 20.000 → 360,00 marj) doğrulama vektörüdür.

## `PUT /{merchantId}/status` — DEĞİŞMEZ