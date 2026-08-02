# Phase 1 — Data Model: BinCard Katalog Admin UI (009)

Yeni **persist edilen** varlık YOK — 008'in `BinCard` document'i üzerinde okuma. Aşağıdakiler API
yanıt/istek DTO'ları (Payment.Api) ve Admin görüntü modelleri. Kaynak: [[008]] `BinCard`
(BinNumber, BankCode, CardType, CardBrand, CardProgram, Commercial) + enum'lar.

## Payment.Api — yanıt/istek DTO'ları

### `BinCardDetailResponse` (US1, `GET api/v1/bin-cards/{bin}`)

Tekil BIN detayı: ham katalog alanları + türetilmiş taksit-banka listesi.

| Alan | Tip | Not |
|------|-----|-----|
| `BinNumber` | `string` | Çözülen kayıt (8→6 fallback sonrası gerçek BIN). |
| `BankCode` | `string` | 4 haneli banka kodu. |
| `CardType` | `string` | Enum **adı**: "Debit" / "Credit". |
| `CardBrand` | `string` | Enum adı: "Visa"/"MasterCard"/…/"Unknown". |
| `CardProgram` | `string` | Enum adı: "Bonus"/…/"Unknown". |
| `Commercial` | `bool` | Ticari kart mı. |
| `InstallmentBankCodes` | `IReadOnlyList<string>` | `ResolveBinCard.DeriveInstallmentBankCodes` (008 paritesi; kredi+geçerli program değilse boş). |

Bulunamazsa endpoint `404` / `FeatureObjectResultModel.NotFound` (istisna yok). `class, new()` — Result
sarımı için parametresiz ctor'lu sınıf.

### `BinCardListItem` (US2, liste satırı)

| Alan | Tip | Not |
|------|-----|-----|
| `BinNumber` | `string` | |
| `BankCode` | `string` | |
| `CardType` | `string` | Enum adı. |
| `CardBrand` | `string` | Enum adı. |
| `CardProgram` | `string` | Enum adı. |
| `Commercial` | `bool` | |

> Liste satırında **taksit-banka türetme YOK** (spec assumption) — yalnız ham alanlar.

### Liste filtre + sayfalama girdisi (`GET api/v1/bin-cards` query params)

| Param | Tip | Not |
|-------|-----|-----|
| `bankCode` | `string?` | Exact eşleşme. |
| `cardProgram` | `string?` | Enum adı; `Enum.TryParse` başarısız → filtre yok sayılır. |
| `cardType` | `string?` | Enum adı (Debit/Credit). |
| `cardBrand` | `string?` | Enum adı. |
| `commercial` | `bool?` | |
| `page` | `int` | 1-tabanlı; `< 1` → 1. |
| `pageSize` | `int` | Varsayılan 25; sunucu üst sınırı (ör. 100) ile kırpılır. |

Yanıt: `FeaturePagedResultModel<BinCardListItem>` (Data + sayfalama metası: toplam kayıt/sayfa/sayfa
boyutu — `PagedList.Core` `IPagedList`).

## Kurallar / mantık (saf, test edilir)

- **Enum ad eşleme**: yanıt `enum.ToString()`; filtre `Enum.TryParse<TEnum>(value, ignoreCase, out)`.
  Parse edilemeyen filtre değeri → o filtre uygulanmaz (kullanıcıya boş sonuç değil, o kısıt yok).
  *(Tercih plan/tasks'ta netleşir; varsayılan: tanınmaz enum filtresi → 0 sonuç, çünkü kullanıcı
  geçerli seçenekler arasından seçer; serbest metin banka kodu hariç.)*
- **Sayfalama sınırı**: `pageSize` sunucuda üst sınırla kırpılır → tam döküm imkânsız (SC-002).
- **Detay taksit-banka paritesi**: `ResolveBinCard.DeriveInstallmentBankCodes` (008) yeniden kullanılır
  → SC-003.
- **Filtre birleşimi**: aktif filtreler AND; hiçbiri yoksa (sayfalı) tüm katalog.

## Admin — görüntü modelleri (Clients/ApiModels.cs)

Payment.Api DTO'larının Admin karşılıkları (`ApiResult<T>` ile sarılır):

- `BinCardDetail` — `BinCardDetailResponse` alanları (string enum adları + `List<string>`
  InstallmentBankCodes).
- `BinCardListItem` — liste satırı.
- `BinCardListResponse` — `List<BinCardListItem> Items` + sayfalama metası (`TotalCount`, `PageNumber`,
  `PageSize`, `PageCount`).
- `BinCardListFilter` — sorgu bağlaması (bankCode/cardProgram/cardType/cardBrand/commercial/page).

**Enum → Türkçe etiket** (Admin sunum yardımcısı, saf): "Credit"→"Kredi", "Debit"→"Banka",
"Unknown"→"Bilinmiyor", program/marka adları okunur etiketlere; tanınmayan ad → adın kendisi (çökme yok).

## İlişkiler

- Kaynak: Payment BC `BinCard` document (008) — Admin yalnız API üzerinden okur, DB'ye erişmez.
- Taksit-banka türetme: `ResolveBinCard` (008) — detay ucu tüketir.
- Admin `ApiResult`/`ApiClientBase` (mevcut) — transport hatası → dostça sunucu hatası (FR-012).