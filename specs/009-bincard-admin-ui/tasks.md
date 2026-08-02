---
description: "Task list for BinCard Katalog Görüntüleme Ekranları (Admin) (009)"
---

# Tasks: BinCard Katalog Görüntüleme Ekranları (Admin)

**Input**: Design documents from `/specs/009-bincard-admin-ui/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/bincard-admin-api.md, quickstart.md

**Tests**: Saf domain birim testi yalnız US2 filtre-parse/clamp mantığı için (proje kuralı). Razor
Pages / HTTP / BFF / DB round-trip birim testi YOK — quickstart ile elle (005 BFF smoke deseni).

**Organization**: Görevler user story'ye göre gruplu. US1 (tekil çözüm) = MVP; US2 (filtreli sayfalı
liste) ikinci artım. İkisi bağımsız test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralel çalışabilir (farklı dosya, bağımlılık yok)
- Yol: `src/services/Payment.Api/...`, `src/ui/Admin/...`, `src/aspire/AppHost/...`, `tests/Payment.Api.Tests/...`

---

## Phase 1: Setup

**Purpose**: Admin'in payment-api'ye ulaşabilmesi için orkestrasyon bağı.

- [X] T001 `src/aspire/AppHost/AppHost.cs`: `payment-api` node'unu bir değişkene al (`var paymentApi =
  builder.AddProject<Projects.Payment_Api>("payment-api")...`) ve `admin-web` node'una
  `.WithReference(paymentApi).WaitFor(paymentApi)` ekle (şu an yalnız merchant/commission referanslı).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: İki ekranın da paylaştığı Admin altyapısı — typed client, görüntü modelleri, enum etiket,
menü. Bu faz bitmeden US ekran sayfaları çalışmaz.

**⚠️ CRITICAL**: Foundational bitmeden hiçbir user story ekranı tamamlanamaz.

- [X] T002 [P] Admin görüntü modelleri `src/ui/Admin/Clients/ApiModels.cs` (mevcut dosyaya ekle):
  `BinCardDetail` (binNumber, bankCode, cardType, cardBrand, cardProgram string adları, commercial,
  `List<string>` installmentBankCodes), `BinCardListItem` (aynı alanlar taksitsiz), `BinCardListResponse`
  (`List<BinCardListItem> Items` + TotalCount/PageNumber/PageSize/PageCount), `BinCardListFilter`
  (bankCode/cardProgram/cardType/cardBrand string?, commercial bool?, page int). data-model.md.
- [X] T003 [P] Enum→Türkçe etiket yardımcısı `src/ui/Admin/PageModels/BinCardLabels.cs` (saf, sunum):
  "Credit"→"Kredi", "Debit"→"Banka", "Unknown"→"Bilinmiyor", program/marka adları okunur etiket;
  tanınmayan ad → adın kendisi (çökme yok). Açılır liste seçenekleri (program/marka/tip) için de kaynak.
- [X] T004 Typed client `src/ui/Admin/Clients/BinCardApiClient.cs` — `IBinCardApiClient` +
  `BinCardApiClient : ApiClientBase`: `GetDetailAsync(string bin)` → `ApiResult<BinCardDetail>`
  (`GET /api/v1/bin-cards/{bin}`); `ListAsync(BinCardListFilter)` → `ApiResult<BinCardListResponse>`
  (`GET /api/v1/bin-cards?...` query string). `SettlementAccountApiClient` deseni. (T002'ye bağlı)
- [X] T005 `src/ui/Admin/Program.cs`: `builder.Services.AddHttpClient<IBinCardApiClient, BinCardApiClient>(
  client => client.BaseAddress = new Uri("http://payment-api"));`. (T004'e bağlı)
- [X] T006 [P] `src/ui/Admin/Pages/Shared/_Layout.cshtml`: navigasyona "BIN Kataloğu" bağlantısı
  (Index sayfasına) ekle.

**Checkpoint**: Admin payment-api'ye bağlı; modeller/etiketler hazır — story ekranları başlayabilir.

---

## Phase 3: User Story 1 - Bir BIN'i çöz ve kart bilgisini gör (P1) 🎯 MVP

**Goal**: Operatör BIN (6/8 hane) girer → banka/tip/marka/program/ticari + taksit-banka listesi tek
detayda; bulunamazsa "yok"; geçersiz giriş TR doğrulama; 8→6 fallback.

**Independent Test**: Bilinen kredi BIN → tam detay (008 paritesi); banka kartı → taksit-banka boş;
999999 → "yok"; 8 hane → ilk 6; geçersiz giriş → TR mesaj (çağrı yok).

### Implementation

- [X] T007 [US1] `ResolveBinCard` yeniden kullanan detay sorgusu — `src/services/Payment.Api/Domains/
  BinCards/Features/Queries/GetBinCardDetail.cs` (record `GetBinCardDetailQuery(string Bin)` +
  `BinCardDetailResponse` (class, new()) + Handler `IQuerySession`). Hedef BinCard'ı 8→6 fallback ile
  bul (yoksa NotFound); ham alanları eşle, enum'ları `ToString()`; `InstallmentBankCodes` için
  `ResolveBinCard.DeriveInstallmentBankCodes` (aynı program kayıtları) kullan (008 paritesi). (008
  ResolveBinCard'a bağlı)
- [X] T008 [US1] `src/services/Payment.Api/Domains/BinCards/BinCardEndpointExtension.cs`: mevcut
  `GET {bin}` ucunu **detail döndürecek** şekilde güncelle — `GetBinCardDetail` sonucu
  `FeatureObjectResultModel<BinCardDetailResponse>` / 404. (Eski bare-CardInfo dönüşü kalkar; iç HTTP
  tüketicisi yok.) (T007'ye bağlı)
- [X] T009 [US1] Admin sayfası `src/ui/Admin/Pages/BinCards/Resolve.cshtml` + `Resolve.cshtml.cs`
  (`BasePageModel`, `IBinCardApiClient`): BIN giriş formu + istemci-tarafı doğrulama (boş/rakam-dışı/
  6'dan kısa/8'den uzun → TR mesaj, çağrı yok); başarı → detay (Türkçe etiketlerle, `BinCardLabels`);
  taksit-banka kod listesi; 404 → "bu BIN katalogda yok"; transport hatası → TR sunucu hatası. (T004,
  T003, T008'e bağlı)

**Checkpoint**: US1 uçtan uca çalışır — MVP (US2'siz teslim edilebilir).

---

## Phase 4: User Story 2 - Katalogu sayfalı listele ve filtrele (P2)

**Goal**: Operatör kataloğu banka kodu/program/tip/marka/ticari (AND) filtreler ve sayfa sayfa gezer;
~9957 kayıt asla tek yanıtta değil.

**Independent Test**: Banka kodu filtresi → yalnız o banka sayfalı; +program → kesişim; tip/marka/
ticari daraltır; uymayan kombinasyon → "sonuç yok"; sayfa ileri/geri doğru dilim.

### Implementation

- [X] T010 [US2] Liste sorgusu `src/services/Payment.Api/Domains/BinCards/Features/Queries/
  ListBinCards.cs` (record `ListBinCardsQuery(bankCode?, cardProgram?, cardType?, cardBrand?,
  commercial?, page, pageSize)` + Handler `IQuerySession`): **saf filtre-hazırlama yardımcısı ayrı**
  (enum `TryParse`; `pageSize` üst sınıra clamp; `page < 1 → 1`; hangi filtreler aktif); handler koşullu
  `Where` + Marten `ToPagedList(page, pageSize)` → `FeaturePagedResultModel<BinCardListItem>` (enum'lar
  `ToString()`). data-model.md + contracts. (008 BinCard'a bağlı)
- [X] T011 [US2] `src/services/Payment.Api/Domains/BinCards/BinCardEndpointExtension.cs`: segmentsiz
  `GET api/v1/bin-cards` (query paramları) → `ListBinCards`; `{bin}` detay ucuyla çakışmaz. (T010,
  T008'e bağlı — aynı dosya, sıralı)
- [X] T012 [US2] Admin sayfası `src/ui/Admin/Pages/BinCards/Index.cshtml` + `Index.cshtml.cs`
  (`BasePageModel`, `IBinCardApiClient`, `[BindProperty(SupportsGet=true)]` filtreler + page): filtre
  formu (banka kodu metin; program/tip/marka açılır — `BinCardLabels`; ticari üçlü); tablo (Türkçe
  etiketli); sayfalama (ileri/geri + göstergesi); boş → "sonuç yok"; hata → TR sunucu hatası. (T004,
  T003, T011'e bağlı)
- [X] T013 [P] [US2] Birim test `tests/Payment.Api.Tests/ListBinCardsFilterTests.cs` — saf filtre-hazırlama
  yardımcısı: enum ad parse (geçerli/geçersiz), `pageSize` clamp (0/negatif/aşırı → sınır), `page<1→1`,
  aktif-filtre seçimi. (T010'a bağlı)

**Checkpoint**: İki ekran da bağımsız çalışır; katalog filtreli/sayfalı gezilir.

---

## Phase 5: Polish & Cross-Cutting

- [X] T014 [P] `dotnet build` (Payment.Api + Admin + AppHost) + `dotnet test tests/Payment.Api.Tests`
  yeşil.
- [ ] T015 `quickstart.md` senaryolarını elle koştur (Aspire: US1 detay + parite, US2 filtre/sayfa,
  payment-api kapalıyken TR hata).

---

## Dependencies & Execution Order

- **Setup (T001)**: bağımsız (orkestrasyon).
- **Foundational (T002-T006)**: Setup sonrası; TÜM story ekranlarını bloklar. T002/T003/T006 [P];
  T004 T002'ye, T005 T004'e bağlı.
- **US1 (T007-T009)**: Foundational sonrası. T007→T008 (aynı endpoint dosyası mantığı) sıralı; T009
  T004+T003+T008'e bağlı.
- **US2 (T010-T013)**: Foundational sonrası; US1'den bağımsız KODLANIR ama T011 endpoint dosyasını
  T008 ile paylaşır → T008 sonrası (sıralı). T012 T011'e; T013 [P] T010'a bağlı.
- **Polish (T014-T015)**: istenen story'ler bitince.

### Story bağımsızlığı
- US1 birim testi yok (008 `ResolveBinCard` paritesi zaten test edilir) — uçtan uca quickstart.
- US2 filtre-parse/clamp saf → birim test (T013); DB/sayfa akışı quickstart.

---

## Parallel Example

```bash
# Foundational paralel:
T002 ApiModels.cs · T003 BinCardLabels.cs · T006 _Layout.cshtml

# Endpoint dosyası (BinCardEndpointExtension.cs) T008 + T011 → SIRALI (aynı dosya)
```

---

## Implementation Strategy

**MVP** = Phase 1 + 2 + US1 (tekil çözüm ekranı gerçek veriyle çalışır). US2 (filtreli liste) ikinci
artımdır.

1. Setup + Foundational → Admin payment-api'ye bağlı, modeller/etiket hazır.
2. US1 (Resolve) → BIN detay ekranı uçtan uca → **MVP**.
3. US2 (Index) → filtreli sayfalı liste.
4. Polish → build+test yeşil, quickstart elle.

---

## Notes

- [P] = farklı dosya, bağımlılık yok.
- Test = yalnız US2 saf filtre-parse/clamp; gerisi quickstart (005 BFF smoke deseni).
- Admin backend'e kural sızdırmaz: türetme/8→6/filtre Payment.Api'de; Admin yalnız gösterir + TR etiket.
- `GET {bin}` yanıtı CardInfo'dan detail'e zenginleşir; iç HTTP tüketicisi yok → kırılma yok.
- Enum yanıtta string ad; Admin Payment.Api enum tipine bağımlı değil.
- Her task/mantıksal grup sonrası commit.