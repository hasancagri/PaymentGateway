# API Contract: Merchant CRUD + Statü (023)

Taban: `Merchant.Api` — Minimal API, URL segment versiyonlama (`/api/v1/...`).
Kimlik: Bearer JWT (Identity.Server, issuer `https://localhost:5101`).
Tüm yanıt gövdeleri `FeatureObjectResultModel<T>` zarfındadır (`Data`, `IsSuccess`,
`Messages[]` — `MessageText` Türkçe). Beklenen hatalar 200 + `IsSuccess=false` zarfıyla
döner (Result pattern); 401/403 yalnız kimlik/yetki katmanından gelir.

## Policy özeti

| Uç | Scope | Düzlem |
|----|-------|--------|
| POST `/api/v1/merchants` | `merchant.write` | `AdminPlaneOnly` |
| PUT `/api/v1/merchants/{merchantId}` | `merchant.write` | `AdminPlaneOnly` |
| PUT `/api/v1/merchants/{merchantId}/status` | `merchant.write` | `AdminPlaneOnly` |
| GET `/api/v1/merchants/{merchantId}` | `merchant.read` | `MerchantScoped` |
| GET `/api/v1/merchants` | `merchant.read` | `AdminPlaneOnly` |

`AdminPlaneOnly`: `merchant_id` claim'li (merchant) token REDDEDİLİR.
`MerchantScoped`: `merchant_id` claim'i varsa route `{merchantId}` ile eşleşmek zorunda
(uyuşmazlık 403); claim'siz admin token'ı serbest.

## 1. Merchant oluştur

`POST /api/v1/merchants`

```json
{
  "type": "Personal | PrivateCompany | LimitedOrJointStockCompany",
  "name": "string",
  "email": "string",
  "gsmNumber": "string",
  "address": "string",
  "iban": "TR-IBAN",
  "contactName": "string",
  "contactSurname": "string",
  "identityNumber": "string | null",
  "taxOffice": "string | null",
  "taxNumber": "string | null",
  "legalCompanyTitle": "string | null"
}
```

Başarı `Data` (MerchantKey'in TEK göründüğü yer — SC-004):

```json
{ "merchantId": "guid", "merchantKey": "mk_..." }
```

Beklenen hatalar: zorunlu alan boş, e-posta biçimi, IBAN mod-97, tip-uyum matrisi ihlali
(alan bazlı `MessageItem`; kayıt oluşmaz).

Yan etki: commit'le atomik `MerchantCreated(merchantId, merchantKey, "Active")` yayını
(`merchant.lifecycle` fanout → Identity istemci oluşturur).

## 2. Merchant güncelle

`PUT /api/v1/merchants/{merchantId}` — gövde: oluşturma ile aynı alan seti.

Başarı `Data`: güncel merchant görünümü (bkz. §4 — MerchantKey YOK).
Beklenen hatalar: kayıt bulunamadı + oluşturmadaki tüm doğrulamalar.
`merchantId`/`merchantKey`/`status`/`subMerchantKey` bu uçtan değişmez. Event yayını YOK.

## 3. Statü değiştir

`PUT /api/v1/merchants/{merchantId}/status`

```json
{ "status": "Active | Passive | Suspended" }
```

Başarı `Data`: `{ "merchantId": "guid", "status": "..." }`.
Aynı statüye geçiş: başarı (idempotent), event yayını YOK.
Gerçek değişiklik: commit'le atomik `MerchantStatusChanged(merchantId, newStatus)` yayını
→ Identity izinleri açar/kapar (yalnız Active token alır).
Merchant'ın kendi token'ı: 403 (`AdminPlaneOnly`).

## 4. Tekil merchant

`GET /api/v1/merchants/{merchantId}`

Başarı `Data` (MerchantKey alanı yanıt tipinde HİÇ YOK):

```json
{
  "merchantId": "guid",
  "status": "Active",
  "type": "Personal",
  "name": "...", "email": "...", "gsmNumber": "...", "address": "...",
  "iban": "...", "contactName": "...", "contactSurname": "...",
  "identityNumber": null, "taxOffice": null, "taxNumber": null,
  "legalCompanyTitle": null, "subMerchantKey": null
}
```

Merchant kendi token'ıyla yalnız kendi `merchantId`'sini çağırabilir (başkası → 403).

## 5. Merchant listesi

`GET /api/v1/merchants` — tam liste (sayfalama yok — spec varsayımı).
`Data`: §4 görünümünün dizisi. Merchant token'ı: 403 (`AdminPlaneOnly`).

## Event sözleşmesi (mevcut — DEĞİŞMEZ)

`Shared.IntegrationEvents` (yayın kayıtları Merchant.Api Program.cs'te hazır):

```csharp
record MerchantCreated(Guid MerchantId, string MerchantKey, string Status);   // oluşturmada
record MerchantStatusChanged(Guid MerchantId, string NewStatus);              // gerçek statü değişiminde
```

Tüketici: `Identity.Server.EventHandlers.MerchantClientEventHandler` (idempotent upsert;
token verme yalnız Active/Provisioning — bu faz yalnız Active üretir). `MerchantProvisioned`
bu fazda yayınlanmaz.
