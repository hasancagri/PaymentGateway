# Contracts: Reference.Api (v1) + Integration Event

Tüm HTTP uçları `api/v{version:apiVersion}/...`, Minimal API + `*EndpointExtension`, `FeatureObjectResultModel<T>` sarımı. v1 **salt-okuma** (yazma yok — Q2 A). Yetki yok (proje geneli erteleme).

## HTTP — Read API

### GET `api/v1/countries`
- Yanıt: `200` `{ items: [{ code, name }] }` (küçük liste, sayfasız).

### GET `api/v1/cities?countryCode={code}`
- `countryCode` opsiyonel filtre.
- Yanıt: `200` `{ items: [{ code, name, countryCode }] }`.

### GET `api/v1/mccs?page={n}&pageSize={m}`
- Sayfalı (009 pager deseni: Default 25, Max 100, page<1→1).
- Yanıt: `200` `{ items: [{ code, name }], totalCount, page, pageSize, pageCount }`.

### GET `api/v1/banks?page={n}&pageSize={m}`
- Sayfalı (aynı pager).
- Yanıt: `200` `{ items: [{ code, name }], totalCount, page, pageSize, pageCount }`.

### GET `api/v1/reference/snapshot`
- Boot-zamanı toplu okuma (sayfasız).
- Yanıt: `200`
```json
{
  "countries": [{ "code": "TR", "name": "Türkiye" }],
  "cities":    [{ "code": "34", "name": "İstanbul", "countryCode": "TR" }],
  "mccs":      [{ "code": "5411", "name": "Grocery Stores" }],
  "banks":     [{ "code": "0062", "name": "..." }]
}
```

## Integration Event — `ReferenceDataUpdated`

- Exchange: `reference.data-updated` (fanout, `RabbitMqConstants.ReferenceDataUpdated.Exchange`).
- Publisher: Reference.Api (publish-then-save; diff — yalnız değişen; seed'de tüm set).
- Kontrat (`Shared.IntegrationEvents`):
```
ReferenceDataUpdated(string Kind, IReadOnlyList<ReferenceItem> Items)
ReferenceItem(string Code, string Name, string? CountryCode)
Kind ∈ { "Country", "City", "Mcc", "Bank" }
```
- Consumer (Merchant, Commission): durable queue bind (`merchant.reference-sync`, `commission.reference-sync`), `Handle(ReferenceDataUpdated)` idempotent upsert (Code anahtar); başarısız → sınırlı retry → DLQ.

## Tüketici arabirim sözleşmesi (DEĞİŞMEZ — FR-010)

Merchant tarafı mevcut arabirimler korunur; yalnız implementasyon read-model'i okur:
```
IMccLookup.Exists(code) / NameOf(code)
ICountryLookup.Exists(code) / NameOf(code)
ICityLookup.Exists(code) / NameOf(code) / BelongsTo(cityCode, countryCode)
IBankCodeLookup.Exists(code) / NameOf(code)
```
Çağıran iş kodu (CreateMerchant, Create/UpdateSettlementAccount, GetSettlementAccount/s) hiç değişmez.

## SharedKernel sözleşmesi

`CardBrand` / `CardType` (kanonik değerler — data-model.md A). Payment.Api + Commission.Api ProjectReference verir; yerel enum kopyaları silinir. Commission `Criteria.FromCodes` string parse **case-insensitive** (eski `VISA`/`CREDIT` de kabul), `GetCriteriaOptions` kanonik isimleri döner.