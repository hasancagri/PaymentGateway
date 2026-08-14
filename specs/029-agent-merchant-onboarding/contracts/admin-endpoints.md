# Contract: Admin Uçları (Merchant.Api `/api/v1/register-requests`)

**Auth**: tümü `merchant.write`/`merchant.read` + `AdminPlaneOnly` (claim'li merchant token giremez).
Tüketici: Admin BFF (`admin-ui` istemcisi, `AdminTokenHandler`).

## `GET /api/v1/register-requests` — liste (merchant.read + AdminPlaneOnly)

Tüm başvurular, `CreatedTime` DESC (tarihçe dahil; sayfalama yok — düşük hacim varsayımı).

```json
{ "requests": [ {
  "requestId": "guid", "status": "Pending|Approved|Rejected",
  "type": "Personal|...", "name": "...", "email": "...", "gsmNumber": "...",
  "iban": "...", "contactName": "...", "contactSurname": "...",
  "rejectReason": null, "merchantId": null, "createdTime": "..." } ] }
```

## `POST /api/v1/register-requests/{requestId:guid}/approve` — onay (merchant.write + AdminPlaneOnly)

Gövde yok. Başarı: `{ "requestId": "...", "merchantId": "..." }`.
Etki: `Merchant.Create` (Active) + `MerchantCreated` outbox (Identity OpenIddict senkronu) +
request `Approved(MerchantId)`. Pending değilse `INVALID_OPERATION_ERROR`; yoksa `RECORD_NOT_FOUND`.

## `POST /api/v1/register-requests/{requestId:guid}/reject` — red (merchant.write + AdminPlaneOnly)

Gövde: `{ "reason": "..." }` (zorunlu, boş olamaz). Başarı: `{ "requestId": "...", "status": "Rejected" }`.
Pending değilse `INVALID_OPERATION_ERROR`; yoksa `RECORD_NOT_FOUND`.

## Admin UI (BFF — kural sızdırmaz)

`Pages/RegisterRequests/Index`: liste tablosu (durum, tip, isim, e-posta, tarih, red nedeni /
merchant linki) + Pending satırlarda Onayla butonu ve neden input'lu Reddet formu. Nav: "Merchant
Talepleri". `RegisterRequestApiClient` (`http://merchant-api`, `AdminTokenHandler`).
