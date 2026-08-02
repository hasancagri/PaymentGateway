# Sözleşmeler — BinCard Katalog Admin (009)

İki Payment.Api okuma ucu (Admin tüketir) + Admin UI kontratı. Enum'lar **string ad** olarak taşınır.

## 1. GetBinCardDetail (US1) — `GET api/v1/bin-cards/{bin}`

008'in debug ucunu zenginleştirir (iç HTTP tüketicisi yok → kırılma yok).

**Girdi**: yol parametresi `bin` (string, 6 veya 8 hane).

**Çıktı** (200) — `BinCardDetailResponse`:
```json
{
  "binNumber": "365770",
  "bankCode": "0124",
  "cardType": "Credit",
  "cardBrand": "Troy",
  "cardProgram": "Bonus",
  "commercial": true,
  "installmentBankCodes": ["0124", "0062"]
}
```
- 8 hane → tam eşleşme yoksa ilk 6 ile çözülür; `binNumber` gerçek çözülen kaydı yansıtır.
- Kredi + geçerli program değilse `installmentBankCodes` boş.
- **Bulunamazsa**: `404` (`FeatureObjectResultModel.NotFound`; istisna/sahte-default yok).

**Tüketici**: Admin `Resolve` sayfası. Taksit-banka `ResolveBinCard.DeriveInstallmentBankCodes` (008
paritesi).

## 2. ListBinCards (US2) — `GET api/v1/bin-cards`

Çok-alanlı filtre + sayfalı ham katalog listesi.

**Query parametreleri** (hepsi opsiyonel, filtreler AND):
```
bankCode=0062&cardProgram=Bonus&cardType=Credit&cardBrand=Troy&commercial=true&page=1&pageSize=25
```
- Enum filtreleri **ad** (ignoreCase parse); `bankCode` exact.
- `page` 1-tabanlı (`<1`→1); `pageSize` varsayılan 25, sunucu üst sınırıyla (ör. 100) kırpılır.

**Çıktı** (200) — `FeaturePagedResultModel<BinCardListItem>`:
```json
{
  "isSuccess": true,
  "data": [
    { "binNumber": "374421", "bankCode": "0062", "cardType": "Credit", "cardBrand": "Amex", "cardProgram": "Bonus", "commercial": false }
  ],
  "metaData": { "totalItemCount": 128, "pageNumber": 1, "pageSize": 25, "pageCount": 6 }
}
```
*(Metadata alan adları `PagedList.Core`/`FeaturePagedResultModel` çıktısına göre; Admin karşılığı
`BinCardListResponse` bunları `TotalCount/PageNumber/PageSize/PageCount`'a eşler.)*

**Davranış**:
- Filtresiz → tüm katalog **sayfalı** (tam döküm asla tek yanıtta).
- Eşleşme yok → boş `data` + metadata (hata değil).
- Route ayrımı: segmentsiz `GET /` = liste; `GET /{bin}` = detay (çakışma yok).

## 3. Seed/Import (008 — değişmez)

009 hiçbir yazma yapmaz. Katalog güncelleme yalnız 008 `POST api/v1/bin-cards/import`. 009 salt-okuma.

## 4. Admin UI kontratı

### Resolve sayfası (US1) — `Pages/BinCards/Resolve`
- Girdi: BIN (6/8 hane). İstemci-tarafı doğrulama: boş/rakam-dışı/6'dan kısa/8'den uzun → Türkçe mesaj,
  çağrı yapılmaz (FR-005).
- Çıktı: banka kodu, kart tipi (Türkçe: Kredi/Banka), marka, program, ticari (Evet/Hayır), taksit-banka
  kod listesi. `404` → "bu BIN katalogda yok" (FR-003). Transport hatası → Türkçe sunucu hatası (FR-012).

### Index sayfası (US2) — `Pages/BinCards/Index`
- Filtre kutuları: banka kodu (metin), kart programı/tipi/markası (açılır liste, enum adları + Türkçe
  etiket), ticari (üçlü: hepsi/evet/hayır). Sayfa gezinme (ileri/geri + sayfa göstergesi).
- Tablo: BIN, banka kodu, kart tipi, marka, program, ticari — hepsi Türkçe etiketli.
- Boş sonuç → "sonuç yok"; hata → Türkçe sunucu hatası. Tek seferde tüm katalog yüklenmez.

### Ortak
- Türkçe metin (resx yok, sabit). Enum→etiket eşlemesi Admin sunum yardımcısında.
- Yetki yok (proje-geneli erteleme) — risk işaretli.
- Admin backend'e kural sızdırmaz: türetme/8→6/filtre mantığı Payment.Api'de; Admin yalnız gösterir.