# Phase 1 Data Model: Reference Data BC + SharedKernel

## A. SharedKernel — kanonik enum'lar (`src/others/SharedKernel/CardTaxonomy/`)

### CardBrand (kanonik = Payment seti)
```
Unknown = -1, Visa = 0, MasterCard = 1, Troy = 2, Amex = 3, Discover = 4, Unionpay = 5, JCB = 6
```
- Payment.Api + Commission.Api yerel kopyayı siler, buna referans verir.
- Payment BinCard verisi değerleri korur (kanonik = Payment) → **veri dönüşümü yok**.

### CardType (kanonik = Payment + Prepaid superset)
```
Unknown = -1, Debit = 0, Credit = 1, Prepaid = 2
```
- Payment (Debit=0, Credit=1) korunur; Commission'ın PREPAID'ini korumak için `Prepaid=2` eklendi (bkz research R5, kullanıcı onayı gerektirir).
- `Unknown=-1` opsiyonel (Payment'ta yok; eklenirse geriye uyumlu, mevcut veriyi etkilemez).

### Kapsam dışı (bu feature'da SharedKernel'e alınmaz)
- `CardProgram` (yalnız Payment, 14 değer) — dublicate değil, Payment'ta kalır.
- `TransactionRegion` (yalnız Commission, DOMESTIC/INTERNATIONAL) — dublicate değil, Commission'da kalır.

## B. Reference.Api — kaynak-of-truth aggregate'leri (`referenceManagement` şeması)

Hepsi `AggregateRoot`'tan türer; statik `Create` fabrikası + invariant (anemik değil). v1 salt-okuma → dış yazma yok, ama aggregate invariant'ları seed/ileride yönetim için tanımlı.

### Country
| Alan | Tip | Kural |
|------|-----|-------|
| Code | string | İş anahtarı; ISO-benzeri, boş değil, normalize (upper). Marten Identity. |
| Name | string | Boş değil. |
- Invariant: Code formatı geçerli; Name dolu. `Create(code, name)`.

### City
| Alan | Tip | Kural |
|------|-----|-------|
| Code | string | İş anahtarı; boş değil. |
| Name | string | Boş değil. |
| CountryCode | string | Var olan bir Country'ye işaret etmeli (çapraz tutarlılık — `BelongsTo` bunun üstünde çalışır). |
- Invariant: Code/Name dolu; CountryCode dolu. `Create(code, name, countryCode)`.

### Mcc
| Alan | Tip | Kural |
|------|-----|-------|
| Code | string | İş anahtarı; 4 hane (`^\d{4}$`). Marten Identity. |
| Name | string | Boş değil. |
- Invariant: 4-hane kod; Name dolu.

### Bank (yalnız code→ad — komisyon-özel öznitelik BURADA DEĞİL)
| Alan | Tip | Kural |
|------|-----|-------|
| Code | string | İş anahtarı; 4 hane. Marten Identity. |
| Name | string | Boş değil. |
- Not: `SupportedInstallments`/aktiflik Reference'a **taşınmaz** — Commission'da kalır (Q3). Reference Bank saf katalogdur.

**Seed**: her aggregate için embedded JSON (`Domains/<Entity>/Data/<entity>.json`), int kod değil düz string code+name (Bank/MCC), City için +countryCode. Bank seed = mevcut `BankCatalog` 63 kaydı (Merchant+Commission kopyalarından türetilir). `ReferenceSeeder : IInitialData` idempotent (`AnyAsync`).

## C. Tüketici read-model'leri (her BC kendi şemasında — BC izolasyonu)

Reference verisinin yerel izdüşümü. Davranışsız **read-model satırı** (StorefrontView deseni), aggregate değil. Idempotent upsert ile `ReferenceDataUpdated` handler'ından güncellenir.

### Merchant.Api yerel read-model
- `ReferenceCountry(Code, Name)`, `ReferenceCity(Code, Name, CountryCode)`, `ReferenceMcc(Code, Name)`, `ReferenceBank(Code, Name)`.
- Mevcut `IMccLookup`/`ICountryLookup`/`ICityLookup`/`IBankCodeLookup` **implementasyonları** artık bu read-model'i okur (arabirim + çağrı noktaları DEĞİŞMEZ — FR-010).
- **SİLİNİR**: `Domains/Merchants/Lookups/{LookupData, LookupRefs}` (gömülü Country/City/MCC), `Domains/SettlementAccounts/Lookups/BankCatalog.cs` (kopya).

### Commission.Api yerel read-model
- `ReferenceBank(Code, Name)` (yalnız banka gerekli; Country/City/MCC Commission'da kullanılmıyor).
- Banka adı/varlık doğrulaması (`Bank.Create` → ad türetme) artık read-model'den. `Bank` aggregate'i **kalır** ama code→ad'ı yerel katalogdan değil read-model'den alır; `SupportedInstallments` Commission'da kalır.
- **SİLİNİR**: `Domains/Banks/BankCatalog.cs` (code→ad kopya), `Domains/SharedKernel/{CardBrand, CardType}` (→ SharedKernel).

## D. Migration eşleme tabloları (Commission grid, int remap — tek geçiş)

Etkilenen dokümanlar: `BankCommission.Criteria`, `MerchantCommission.Criteria` (varsa). Grid banka **kodu** (string) etkilenmez.

### CardBrand: eski → kanonik
| Eski (Commission) | int | Kanonik | int |
|---|---|---|---|
| VISA | 1 | Visa | 0 |
| MASTERCARD | 2 | MasterCard | 1 |
| TROY | 3 | Troy | 2 |
| AMEX | 4 | Amex | 3 |

### CardType: eski → kanonik
| Eski (Commission) | int | Kanonik | int |
|---|---|---|---|
| CREDIT | 1 | Credit | 1 (aynı) |
| DEBIT | 2 | Debit | 0 |
| PREPAID | 3 | Prepaid | 2 |

**Kritik**: Eşleme **kaynak int'e göre tam sözlük** olarak tek geçişte uygulanır; in-place/sıralı güncelleme yasak (2→1, 3→2 üst üste biner). İdempotent: doküman bir "migrated" işareti taşır ya da migration yalnız eski-şema dokümanlarına uygulanır.

## E. Integration event — `ReferenceDataUpdated` (`Shared.IntegrationEvents`)

Kontrat (öneri — küçük katalog, tam-set yaklaşımı bootstrap ihtiyacını da azaltır):
```
ReferenceDataUpdated(string Kind, IReadOnlyList<ReferenceItem> Items)
  Kind ∈ { "Country", "City", "Mcc", "Bank" }
  ReferenceItem(string Code, string Name, string? CountryCode)   // CountryCode yalnız City
```
- Fanout exchange `reference.data-updated` (RabbitMqConstants).
- Publish-then-save; yalnız değişen kayıtlar (diff) — v1 seed'de tüm set yayılabilir.
- Tüketici idempotent upsert (Code anahtar).

## F. Snapshot — `GET /reference/snapshot`

Boot-zamanı toplu okuma (sayfasız, küçük veri):
```
ReferenceSnapshotResponse(
  IReadOnlyList<CountryDto> Countries,   // Code, Name
  IReadOnlyList<CityDto> Cities,         // Code, Name, CountryCode
  IReadOnlyList<MccDto> Mccs,            // Code, Name
  IReadOnlyList<BankDto> Banks)          // Code, Name
```
Tüketici açılışta read-model boşsa bir kez çeker, upsert eder; sonra event'lerle güncel kalır.