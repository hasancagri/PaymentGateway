# Sözleşmeler — BinCard Kataloğu (008)

Payment.Api içi. Çözümleme = domain query (iç tüketici); import = REST endpoint (operatör).

## 1. ResolveBinCard (query — iç)

BIN → kart bilgisi. Ödeme/taksit akışlarının tükettiği çözüm.

**Girdi**: `binNumber` (string, 6 veya 8 hane)

**Çıktı**: `CardInfo?`
```
CardInfo(string? BankCode, bool IsCreditCard, IReadOnlyList<string> InstallmentBankCodes)
```
- Bulunursa: banka, kredi/banka, türetilmiş taksit-banka listesi (kart bankası başta).
- **Bulunamazsa: `null`** (istisna yok, sahte-default yok).
- 8 hane girdi: tam eşleşme yoksa ilk 6 ile çözülür.

**Tüketici**: `ProcessPayment.LoadCardInfo`, `GetInstallmentOptions` (VPOSClient yerine). Null → çağıran
kendi politikasıyla (reddet / peşin) ele alır.

## 2. Import (endpoint — operatör)

`POST api/v1/bin-cards/import` — yayınlanan BIN listesini idempotent toplu upsert.

**İstek gövdesi**: BIN kayıtları listesi
```json
{
  "items": [
    { "binNumber": "365770", "bankCode": "0124", "cardType": 1, "cardBrand": 2, "commercial": true, "cardProgram": 3 }
  ]
}
```
`cardType/cardBrand/cardProgram` taşınabilir kod (int, CP.VPOS legend değerleri) — sınırda domain enum'a çevrilir.

**Yanıt** (`FeatureObjectResultModel<ImportBinCardsResponse>`):
```json
{ "importedCount": 9850, "updatedCount": 120, "skippedCount": 30, "skippedReasons": ["binNumber eksik: 12 kayıt", ...] }
```

**Davranış**:
- Var olan BinNumber → güncellenir; yeni → eklenir (upsert, kimlik BinNumber).
- Aynı liste ikinci kez → içerik ve sayı değişmez (idempotency, SC-004).
- Geçersiz/eksik kayıt → atlanır + `skipped` sayılır; batch bozulmaz (FR-010).

## 3. Seed (startup — otomatik, endpoint değil)

`BinCardSeeder : IInitialData`. Sistem başlarken katalog boşsa `VPOSClient.AllCreditCardBinList()`'ten
bir kez doldurur (~9900). Doluysa atlanır. Manuel tetik yok.

## Güvenlik / sınır notu

- Import state-değiştiren + hassas; anayasa V açık yetki ister — proje-geneli AUTHZ ertelemesi
  gereği şimdilik korumasız (Identity BC'de kapanır). **Risk işaretli.**
- CP.VPOS tipleri sözleşme sınırını geçmez; import DTO ve seed kaynağı domain enum'una çevrilir.