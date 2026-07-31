# Contract: Banks API (Commission.Api)

Taban: `api/v{version:apiVersion}/banks` (v1). Yanıt zarfı mevcut konvansiyon: başarı → Data düz;
hata → `{ isSuccess:false, messages:[{ property, code }] }`.

## POST /banks — banka oluştur

İstek:
```json
{ "code": "0062", "name": "Garanti BBVA", "supportedInstallments": [1,2,3,6,9,12] }
```
Yanıt 200: `{ "id": "<guid>", "code": "0062" }`
Hata 400: `INVALID_FORMAT` (code≠4 hane), `VALUE_IS_REQUIRED` (name/installments boş),
`INVALID_RANGE` (taksit 1..15 dışı), `RECORD_DUPLICATE` (code zaten var).

## GET /banks?includeInactive=bool — liste

Yanıt 200:
```json
{ "items": [ { "id":"<guid>", "code":"0062", "name":"Garanti BBVA",
              "supportedInstallments":[1,2,3,6,9,12], "isActive":true } ] }
```
`includeInactive` yok/false → yalnız aktif; true → pasifler de.

## GET /banks/{code} — detay

Yanıt 200: tek banka (yukarıdaki item şekli). 404: `RECORD_NOT_FOUND`.

## PUT /banks/{code} — güncelle

İstek: `{ "name": "...", "isActive": true, "supportedInstallments": [1,2,3,6] }`
Yanıt 200: `{ "code": "0062" }`. Code değişmez (rota parametresi otoriter).
Hata: name/installments doğrulama; 404 `RECORD_NOT_FOUND`.

## DELETE /banks/{code} — sil (soft)

Yanıt 200: `{ "code": "0062" }`.
Hata 400: `BANK_HAS_COMMISSIONS` (bağlı komisyon var). 404: `RECORD_NOT_FOUND`.