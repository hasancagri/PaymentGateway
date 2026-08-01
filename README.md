# PaymentGateway (DropShop)

Tedarikçi ürünlerini dropship modeliyle satan e-ticaret sisteminin ödeme altyapısı. Her
mikroservis bir **Bounded Context**; Vertical Slice + CQRS, zengin aggregate'ler, Result pattern.
Altyapı: **Aspire** (orkestrasyon), **Marten** (Postgres document store), **Wolverine** (in-process bus).

## Komutlar

```bash
dotnet build                                             # tüm çözüm (PaymentGateway.slnx)
dotnet run --project src/aspire/AppHost/AppHost.csproj   # sistemi Aspire ile başlat (Postgres + RabbitMQ)
dotnet test tests/Commission.Api.Tests                   # saf domain birim testleri
```

Sistemi her zaman AppHost üzerinden başlatın; servisler bağlantı dizelerini Aspire'dan alır.
Central Package Management açık (sürümler `Directory.Packages.props`); tek istisna `CP.VPOS`.

## Yapı

```text
src/
├── aspire/           AppHost + ServiceDefaults (orkestrasyon)
├── services/
│   ├── Payment.Api   Ödeme BC (CP.VPOS sanal POS ile)
│   ├── Merchant.Api  Merchant onboarding BC
│   └── Commission.Api Komisyon BC (banka + komisyon)
├── ui/Admin          Razor Pages yönetim arayüzü
├── others/           Common (domain base, Result, auth) + Shared (integration event)
└── otherProjects/    CP.VPOS sanal POS kütüphanesi + eski PFApplication referansı (salt-okunur)
```

Bir feature = bir static class (record command/query + Response + Handler + endpoint). Handler'lar
`[Transactional]` + `IDocumentSession`; sonuçlar `FeatureObjectResultModel<T>`/`ResultDomain`
(exception değil). CP.VPOS tipleri slice sınırını geçmez.

## Bounded Context'ler

| BC | Sorumluluk |
|----|-----------|
| **Payment** | Sanal POS ödeme; `BankRouter` maliyet-sıralı banka adayları; `PosAccount` aggregate (banka anlaşması + komisyon). |
| **Merchant** | Merchant onboarding + API key. |
| **Commission** | Banka referansı, banka komisyonları, merchant komisyonları. |

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

Saf domain birim testleri (`tests/Commission.Api.Tests`); banka/dış HTTP çağrıları test edilmez.
`BankTests` (aggregate + katalog), `BulkUpsertCriteriaMatchTests`, `BankCommissionTests`,
`MerchantCommissionTests`.

## Geliştirme akışı

Spec-driven (spec-kit): `specify → plan → tasks → implement`, değişikliklerde `converge`. Feature
artefaktları `specs/<NNN-feature>/`. Yorumlar, mesaj kodları ve commit'ler Türkçe.

## Bilinçli ertelemeler

- Yetkilendirme yok (Identity BC ile gelecek); endpoint'ler şimdilik korumasız.
- Diğer BC'ler (Catalog, Order, Supplier…) tasarım gereği henüz yok; her biri kendi spec döngüsüyle eklenir.