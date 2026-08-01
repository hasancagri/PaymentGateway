# API Contract: Merchant Settlement Hesabı (004)

Base group: `api/v{version:apiVersion}/merchants/{merchantId:guid}/settlement-accounts`
Tag: `settlement-accounts`. API version 1.0. Minimal API, `IMessageBus.InvokeAsync`.
Yetki: YOK (ertelendi). Tenant sınırı: rota `{merchantId}` ile.

Sonuç zarfı: başarı → `200 OK` + `Data`; beklenen hata → `400 Bad Request` + `FeatureObjectResultModel`
(`Messages[]` = `{ Property, Code }`); bulunamadı → `404 Not Found`; beklenmeyen → `500`.

---

## 1. Create — POST `/`

Yeni settlement hesabı ekler.

**Request body**
```json
{
  "bankCode": "0062",
  "iban": "TR330006200000000000000001",
  "accountOwnerName": "ACME Ltd. Şti.",
  "accountNo": "12345678",
  "accountDescription": "Ana TL hesabı"
}
```
`merchantId` rotadan gelir.

**201/200 Response** → `{ "id": "<guid>" }`

**400** — kodlar:
- `COMMON_MESSAGE_VALUE_IS_REQUIRED` (bankCode/iban/accountOwnerName boş)
- `COMMON_MESSAGE_INVALID_FORMAT` (iban TR/mod-97 değil; bankCode 4-hane değil)
- `COMMON_MESSAGE_RECORD_NOT_FOUND` (merchant yok; bankCode katalogda yok)
- `COMMON_MESSAGE_RECORD_DUPLICATE` (aynı merchant'ta aynı iban)

---

## 2. List — GET `/`

Merchant'ın tüm settlement hesapları (yalnız o merchant).

**200 Response**
```json
{
  "accounts": [
    {
      "id": "<guid>",
      "bankCode": "0062",
      "bankName": "Garanti BBVA",
      "iban": "TR330006200000000000000001",
      "accountOwnerName": "ACME Ltd. Şti.",
      "status": "Active"
    }
  ]
}
```

---

## 3. Get — GET `/{accountId:guid}`

Tek hesap ayrıntısı.

**200 Response**
```json
{
  "id": "<guid>",
  "merchantId": "<guid>",
  "bankCode": "0062",
  "bankName": "Garanti BBVA",
  "iban": "TR330006200000000000000001",
  "accountOwnerName": "ACME Ltd. Şti.",
  "accountNo": "12345678",
  "accountDescription": "Ana TL hesabı",
  "status": "Active",
  "createdTime": "2026-08-01T10:00:00Z"
}
```
**404** — hesap yok veya bu merchant'a ait değil.

---

## 4. Update — PUT `/{accountId:guid}`

Hesap bilgilerini günceller (Create ile aynı doğrulamalar).

**Request body** — Create ile aynı alanlar.

**200 Response** → `{ "id": "<guid>" }`
**400** — Create ile aynı kod kümesi. **404** — hesap yok.

---

## 5. Set Status — PATCH `/{accountId:guid}/status`

Hesabı aktif/pasif yapar (silmez).

**Request body**
```json
{ "isActive": false }
```

**200 Response** → `{ "id": "<guid>", "status": "Passive" }`
**404** — hesap yok.

---

## Notlar

- Rota-body tutarlılığı: gövdede `merchantId` yok; her zaman rotadan. Get/Update/SetStatus
  hesabın gerçekten `{merchantId}`'ye ait olduğunu doğrular (tenant sızıntısı yok).
- `bankName` yanıt-türevi (lookup); saklanmaz.
- IBAN yanıtta normalize (boşluksuz, büyük harf) döner.