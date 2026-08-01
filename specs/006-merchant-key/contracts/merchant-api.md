# API Contracts: Merchant Key

Base: `api/v{version}/merchants` (v1.0). Yetki YOK (proje geneli AUTHZ ertelemesi). Yalnız bu
feature'ın etkilediği/eklediği kontratlar. Mesaj metinleri Türkçe (`MessageItem.Code` resource sabiti).

## 1. POST /merchants — Create (değişiklik: response'a MerchantKey)

İstek gövdesi **değişmez** (merchantKey İSTENMEZ; gönderilse yok sayılır):

```json
{
  "name": "Acme Ltd",
  "email": "billing@acme.com",
  "phone": "+905551112233",
  "countryCode": "TR",
  "cityCode": "34",
  "mcc": "5411",
  "webhookUrl": "https://acme.com/webhook"
}
```

**200 OK** — yanıt artık `merchantKey` içerir:

```json
{
  "id": "8f3b...guid",
  "merchantKey": "mk_9f1c2a7b8d3e4f5061728394a5b6c7d8"
}
```

**400 Bad Request** — mevcut doğrulama davranışı (format/lookup) aynı; `FeatureObjectResultModel`
hata zarfı.

Notlar:
- merchantKey sunucu tarafında üretilir, benzersizdir (handler üret-kontrol döngüsü), atomiktir
  (`[Transactional]`).
- İstemci gövdesinde `merchantKey` gönderirse yok sayılır (FR-002).

## 2. GET /merchants/{id:guid} — GetMerchant (değişiklik: response'a MerchantKey)

**200 OK**:

```json
{
  "id": "8f3b...guid",
  "merchantKey": "mk_9f1c2a7b8d3e4f5061728394a5b6c7d8",
  "name": "Acme Ltd",
  "email": "billing@acme.com",
  "phone": "+905551112233",
  "countryCode": "TR", "countryName": "Türkiye",
  "cityCode": "34", "cityName": "İstanbul",
  "mcc": "5411", "mccName": "...",
  "webhookUrl": "https://acme.com/webhook",
  "status": "Active",
  "createdTime": "2026-08-02T10:00:00Z"
}
```

**404 Not Found** — mevcut davranış.

## 3. GET /merchants — GetAllMerchants (değişiklik: her satıra MerchantKey)

**200 OK** — liste öğeleri `merchantKey` içerir (mevcut alanlara ek).

## 4. GET /merchants/by-key/{merchantKey} — GetMerchantByKey (YENİ)

Key ile merchant çözer. `merchantKey` route parametresidir (URL-güvenli, `mk_...`).

**200 OK** — GetMerchant ile aynı şekil (id, merchantKey, temel bilgiler, status, createdTime).

**404 Not Found** — key yok / boş / biçimsiz / merchant soft-deleted. Hata değil, `NotFound()`
sonucu (`COMMON_MESSAGE_RECORD_NOT_FOUND`).

Örnek:

```
GET /api/v1.0/merchants/by-key/mk_9f1c2a7b8d3e4f5061728394a5b6c7d8
```

Endpoint kaydı `MerchantEndpointExtension.AddMerchantGroupEndpointExtension` içine eklenir
(`.GetMerchantByKeyGroupItemEndpoint()`), mevcut Minimal API + `IMessageBus.InvokeAsync` deseniyle.