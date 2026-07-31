# Contract: Merchant.Api

Minimal API, `IMessageBus.InvokeAsync` ile Wolverine handler. Sürüm `v1` (UrlSegment).
Bu dilimde **korumasız** (yetki Identity dilimiyle). Sonuç `FeatureObjectResultModel<T>`:
başarı → `Data`, hata → `IsSuccess=false` + `Messages[]` (BadRequest).

Base: `/api/v1/merchants`

---

## POST /api/v1/merchants — CreateMerchant

Yeni merchant kaydı (source of truth). Key üretimi YOK (Identity dilimi).

**Request**
```json
{
  "name": "Acme Ltd",
  "email": "ops@acme.com",
  "phone": "+902121234567",
  "countryCode": "TR",
  "cityCode": "34",
  "mcc": "5411",
  "webhookUrl": "https://acme.com/webhooks/payments"
}
```

**201 / 200** (Data)
```json
{ "id": "8f3c...guid" }
```

**400** (doğrulama — Result)
```json
{
  "isSuccess": false,
  "messages": [
    { "property": "Email", "code": "COMMON_MESSAGE_INVALID_FORMAT" },
    { "property": "Mcc",   "code": "COMMON_MESSAGE_RECORD_NOT_FOUND" }
  ]
}
```

Doğrulama: isim/email/telefon boş değil; email format; MCC `^\d{4}$` + lookup'ta var; Country/City
lookup'ta var + City↔Country tutarlı; webhook mutlak `http(s)` URL. (Aggregate = format, handler
= lookup varlık — bkz. data-model.)

---

## GET /api/v1/merchants/{id} — GetMerchant

**200**
```json
{
  "id": "8f3c...",
  "name": "Acme Ltd",
  "email": "ops@acme.com",
  "phone": "+902121234567",
  "countryCode": "TR", "countryName": "Türkiye",
  "cityCode": "34", "cityName": "İstanbul",
  "mcc": "5411", "mccName": "Grocery Stores",
  "webhookUrl": "https://acme.com/webhooks/payments",
  "status": "Active",
  "createdTime": "2026-07-31T10:00:00Z"
}
```
Not: `countryName`/`cityName`/`mccName` kod-içi lookup'tan çözülür (saklanmaz).

**404** — bulunamadı (Result, RECORD_NOT_FOUND).

---

## GET /api/v1/merchants — GetAllMerchants

Liste (admin genel görünüm). Basit; sayfalama isteğe bağlı (`IPagerInputModel`).

**200**
```json
[ { "id": "8f3c...", "name": "Acme Ltd", "mcc": "5411", "status": "Active" } ]
```

---

## (Ertelendi — bu dilim yok)

- Key üretimi / gösterimi (`umk_`) → Identity dilimi.
- `deactivate`/`suspend` uçları → orkestrasyon/telafi dilimi (compensating için).
- Yetki (`merchants.manage` scope) → Identity dilimi.