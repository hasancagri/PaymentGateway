# PaymentGateway (DropShop)

Tedarikçi ürünlerini dropship modeliyle satan e-ticaret sisteminin ödeme altyapısı. Her
mikroservis bir **Bounded Context**; Vertical Slice + CQRS, zengin aggregate'ler, Result pattern.
Altyapı: **Aspire** (orkestrasyon), **Marten** (Postgres document store), **Wolverine** (in-process bus).

## Komutlar

```bash
dotnet build                                             # tüm çözüm (PaymentGateway.slnx)
dotnet run --project src/aspire/AppHost/AppHost.csproj   # sistemi Aspire ile başlat (Postgres + RabbitMQ)
dotnet test tests/Merchant.Api.Tests                     # saf domain birim testleri (Merchant)
dotnet test tests/Commission.Api.Tests                   # saf domain birim testleri (Commission)
```

Sistemi her zaman AppHost üzerinden başlatın; servisler bağlantı dizelerini Aspire'dan alır.
Central Package Management açık (sürümler `Directory.Packages.props`); tek istisna `CP.VPOS`.

## Yapı

```text
src/
├── aspire/           AppHost + ServiceDefaults (orkestrasyon)
├── services/
│   ├── Payment.Api   Ödeme BC (CP.VPOS sanal POS ile)
│   ├── Merchant.Api  Merchant BC (onboarding + settlement hesapları)
│   └── Commission.Api Komisyon BC (banka + komisyon)
├── ui/Admin          Razor Pages yönetim arayüzü
└── others/           Common (domain base, Result, auth) + Shared (integration event)
```

Bir feature = bir static class (record command/query + Response + Handler + endpoint). Handler'lar
`[Transactional]` + `IDocumentSession`; sonuçlar `FeatureObjectResultModel<T>`/`ResultDomain`
(exception değil). CP.VPOS tipleri slice sınırını geçmez.

## Bounded Context'ler

| BC | Sorumluluk |
|----|-----------|
| **Payment** | Sanal POS ödeme; `BankRouter` maliyet-sıralı banka adayları; `PosAccount` aggregate (banka anlaşması + komisyon). |
| **Merchant** | Merchant onboarding + API key; settlement (payout) banka hesapları. |
| **Commission** | Banka referansı, banka komisyonları, merchant komisyonları. |

## Merchant BC — Settlement hesapları (feature 004) + Admin ekranları (005)

Merchant'a payout için para yatırılacak banka hesabı yönetimi. `MerchantSettlementAccount` aggregate;
`Merchant` aggregate'ine dokunulmaz, bağ `MerchantId` referansıyla.

### MerchantSettlementAccount aggregate

- **BankCode** (4 hane), **Iban** (normalize saklanır), **AccountOwnerName**, **AccountNo**/
  **AccountDescription** (opsiyonel), **Status** (`Active`/`Passive`, soft — silme yok).
- IBAN doğrulama saf aggregate içinde: `^TR\d{24}$` + **ISO 13616 mod-97**. Yalnız TR (yurtiçi TL).
- Banka referansı yerel `BankCatalog` kopyasına (Commission ile elle senkron) doğrulanır — cross-BC
  çağrı yok. Merchant varlığı + mükerrer IBAN handler'da (Marten sorgu).

### API

| Metod | Yol | Açıklama |
|-------|-----|----------|
| `POST` | `/merchants/{merchantId}/settlement-accounts` | Ekle. |
| `GET` | `…/settlement-accounts` | Merchant'ın hesapları (tenant-scoped). |
| `GET` | `…/settlement-accounts/{accountId}` | Detay (başka merchant → 404). |
| `PUT` | `…/settlement-accounts/{accountId}` | Güncelle. |
| `PUT` | `…/settlement-accounts/{accountId}/status` | `{ isActive }` aktif/pasif. |

Doğrulama kodları: `INVALID_FORMAT` (IBAN/bankCode), `RECORD_NOT_FOUND` (merchant/banka), `RECORD_DUPLICATE`.

### Admin arayüzü (005)

Gateway admin için (merchant self-service değil). Merchant detay → **Settlement Hesapları**: liste
(banka kod+ad, IBAN, sahip, durum), ekleme (banka dropdown Commission katalogundan), düzenleme + aktif/
pasif. Salt-UI — backend'e dokunmaz; API sonucunu `MessageText` ile Türkçe gösterir.

## Commission BC — Banka referansı + komisyon grid (feature 002)

Komisyon BC'ye banka yönetimi ve boşluksuz komisyon girişi eklendi.

### Bank aggregate

- **Code** (4 hane, immutable, iş anahtarı), **Name** (kanonik katalogdan türer, immutable),
  **IsActive**, **SupportedInstallments** (`List<int>`, 1..15, distinct + artan). Sabit `MaxInstallment = 15`.
- `Create(code, installments)` / `Update(isActive, installments)` / `SoftDelete()`.
- **Seed yok** — DB boş başlar; operatör bankaları katalogdan seçerek ekler.

### Kanonik banka katalogu

Seçilebilir bankaların sabit listesi (`BankCatalog`) — CP.VPOS `BankService.AllBanks`'ten kopyalanan
48 banka (Code + Name). CP.VPOS'a çalışma-zamanı bağımlılığı yoktur (`AllBanks` `internal`; değerler
statik gömülü). Operatör banka adı/kodunu **elle yazmaz**, katalogdan seçer; ad ve kod immutable.

### API

| Metod | Yol | Açıklama |
|-------|-----|----------|
| `GET` | `/banks/catalog?onlyAvailable` | Seçilebilir katalog (eklenmişleri eler). |
| `POST` | `/banks` | `{ code, supportedInstallments }` — ad katalogdan türer. |
| `GET` | `/banks?includeInactive` | Liste. |
| `GET` | `/banks/{code}` | Detay. |
| `PUT` | `/banks/{code}` | `{ isActive, supportedInstallments }` — kod/ad değişmez. |
| `DELETE` | `/banks/{code}` | Soft-delete (bağlı komisyon varsa reddedilir). |
| `POST` | `/bank-commissions/bulk` | Atomik toplu upsert (grid kaydı). |
| `GET` | `/bank-commissions/criteria-options` | Kriter enum'ları (tek kaynak). |

Doğrulama kodları: `BANK_NOT_IN_CATALOG` (katalog-dışı kod), `BANK_HAS_COMMISSIONS` (bağlı komisyonlu
banka silinemez), `RECORD_DUPLICATE`, `INVALID_RANGE`.

### Admin arayüzü

- **Bankalar** — katalog selectbox ile ekle, taksit 1..15 checkbox grid; Edit'te kod+ad salt-görünüm.
- **Komisyon grid** — banka seç → `CardBrand × CardType × TransactionRegion × taksit` tam kombinasyon;
  eksik hücreler işaretli; **eksen filtresi** + **20'li sayfalama** + **görünen-boş toplu doldur**;
  tek işlemde kaydet.
- **Komisyon listesi** — banka adı gösterimi + eksen filtresi + 20'li sayfalama.
- Filtre/sayfalama/doldur davranışı jenerik `wwwroot/js/filterable-table.js` modülünde (grid + liste ortak).

## Test

Saf domain birim testleri; handler/HTTP/Razor Pages entegrasyonu test edilmez (quickstart ile elle).

- `tests/Merchant.Api.Tests` — `MerchantTests`, `MerchantSettlementAccountTests` (IBAN mod-97, TR kısıtı,
  durum geçişleri).
- `tests/Commission.Api.Tests` — `BankTests` (aggregate + katalog), `BulkUpsertCriteriaMatchTests`,
  `BankCommissionTests`, `MerchantCommissionTests`.

## Geliştirme akışı

Spec-driven (spec-kit): `specify → plan → tasks → implement`, değişikliklerde `converge`. Feature
artefaktları `specs/<NNN-feature>/`. Yorumlar, mesaj kodları ve commit'ler Türkçe.

## Bilinçli ertelemeler

- Yetkilendirme yok (Identity BC ile gelecek); endpoint'ler şimdilik korumasız.
- Diğer BC'ler (Catalog, Order, Supplier…) tasarım gereği henüz yok; her biri kendi spec döngüsüyle eklenir.