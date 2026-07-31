# Contract: Bank Commissions Bulk Upsert (Commission.Api)

Mevcut `api/v{version}/bank-commissions` grubuna eklenir. Tek-tek `POST /` ve `GET /?bankCode`
korunur (geriye uyum).

## POST /bank-commissions/bulk — toplu ekle/güncelle

Grid kaydı. Seçilen banka + doldurulan hücreler tek atomik işlemde upsert edilir.

İstek:
```json
{
  "bankCode": "0062",
  "items": [
    { "criteria": { "cardBrand":"VISA", "cardType":"CREDIT",
                    "transactionRegion":"DOMESTIC", "installmentCount":3 }, "rate": 1.75 },
    { "criteria": { "cardBrand":"VISA", "cardType":"CREDIT",
                    "transactionRegion":"DOMESTIC", "installmentCount":6 }, "rate": 2.40 }
  ]
}
```

Davranış:
- Banka `bankCode` ile yüklenir; yoksa/pasifse hata.
- Her item: `(bankCode, criteria)` var → `UpdateRate`; yok → `Create`.
- `installmentCount` bankanın `SupportedInstallments`'ında değilse → `INVALID_RANGE`.
- `[Transactional]` — hepsi ya kaydolur ya hiçbiri.

Yanıt 200: `{ "created": 1, "updated": 1 }`
Hata 400: `RECORD_NOT_FOUND` (banka), `INVALID_RANGE` (desteklenmeyen taksit / rate<0),
`INVALID_ENUM_TYPE` (kriter enum'u geçersiz).