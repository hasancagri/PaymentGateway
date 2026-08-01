# Contract: Banks API (Commission.Api)

Taban: `api/v{version:apiVersion}/banks` (v1). Yanıt zarfı mevcut konvansiyon: başarı → Data düz;
hata → `{ isSuccess:false, messages:[{ property, code }] }`.

## GET /banks/catalog?onlyAvailable=bool — kanonik katalog

Seçilebilir bankaların sabit listesi (Code+Name). `onlyAvailable=true` → zaten eklenmiş (`!IsDeleted`)
bankaları eler; yok/false → tüm katalog.

Yanıt 200:
```json
{ "items": [ { "code":"0062", "name":"Garanti BBVA" },
             { "code":"0064", "name":"İş Bankası" } ] }
```

## POST /banks — banka oluştur

Ad ve kod katalogdan gelir; istek yalnız `code` (katalog seçimi) + taksit taşır. `name` gövdede
yer almaz — sunucu katalogdan türetir.

İstek:
```json
{ "code": "0062", "supportedInstallments": [1,2,3,6,9,12] }
```
Yanıt 200: `{ "id": "<guid>", "code": "0062" }`
Hata 400: `INVALID_FORMAT` (code≠4 hane), `BANK_NOT_IN_CATALOG` (code katalogda yok),
`VALUE_IS_REQUIRED` (installments boş), `INVALID_RANGE` (taksit 1..15 dışı),
`RECORD_DUPLICATE` (code zaten eklenmiş).

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

Ad ve kod değişmez (ikisi de katalogdan). İstek yalnız aktiflik + taksit taşır.

İstek: `{ "isActive": true, "supportedInstallments": [1,2,3,6] }`
Yanıt 200: `{ "code": "0062" }`. Code rota parametresinden otoriter; Name katalogdan (dokunulmaz).
Hata: installments doğrulama (`VALUE_IS_REQUIRED`/`INVALID_RANGE`); 404 `RECORD_NOT_FOUND`.

## DELETE /banks/{code} — sil (soft)

Yanıt 200: `{ "code": "0062" }`.
Hata 400: `BANK_HAS_COMMISSIONS` (bağlı komisyon var). 404: `RECORD_NOT_FOUND`.