# Tasks: Merchant Komisyon Grid

**Input**: Design documents from `/specs/003-merchant-commission-grid/`

**Prerequisites**: plan.md, spec.md, data-model.md, contracts/merchant-commissions-api.md, research.md, quickstart.md

**Tests**: Saf domain birim testleri istendi (plan.md + constitution). Banka/dış HTTP ve Admin UI test edilmez.

**Organization**: Görevler kullanıcı hikâyesine göre gruplu; her hikâye bağımsız uygulanıp test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, tamamlanmamış göreve bağımlı değil)
- **[Story]**: Görevin ait olduğu hikâye (US1, US2, US3)
- Açıklamalarda tam dosya yolu var

## Path Conventions

Mevcut yapı korunur: backend `src/services/Commission.Api/`, Admin UI `src/ui/Admin/`, testler
`tests/Commission.Api.Tests/`. Yeni proje/servis yok, yeni NuGet paketi yok (CPM korunur).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Bu dilim mevcut projelere ekleniyor — yeni proje kurulumu yok. Yalnız baseline.

- [X] T001 `dotnet build` ile mevcut çözümün (PaymentGateway.slnx) temiz derlendiğini doğrula (refactor öncesi baseline)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: `MerchantCommission` aggregate'ini kombinasyon-bazlı modele dönüştür. Bu faz bitmeden hiçbir hikâye çalışamaz — tüm command/query bu yeni imzaya dayanır.

**⚠️ CRITICAL**: Bu faz bitmeden US1/US2/US3 başlayamaz.

- [X] T002 `MerchantCommission` aggregate'ini refactor et: `src/services/Commission.Api/Domains/MerchantCommissions/MerchantCommission.cs` — `BankCommissionId` ve `BankCode` alanlarını KALDIR; `Criteria` doğrudan girdiden set edilir; `static ResultDomain<MerchantCommission> Create(Guid merchantId, Criteria criteria, decimal rate)` (`merchantId != Guid.Empty` → `COMMON_MESSAGE_VALUE_IS_REQUIRED`, `criteria` null → `COMMON_MESSAGE_VALUE_IS_REQUIRED`, `rate <= 0` → `COMMON_MESSAGE_INVALID_RANGE`); `ResultDomain UpdateRate(decimal rate)` (`rate <= 0` → `COMMON_MESSAGE_INVALID_RANGE`, `BankCommission` parametresi KALDIR); `RateMustExceedBankRate` yardımcısını sil. XML doc'u yeni modele göre güncelle (data-model.md).
- [X] T003 [P] Kullanılmayan `MERCHANT_RATE_MUST_EXCEED_BANK_RATE` sabitini kaldır: `src/services/Commission.Api/Domains/SharedKernel/CommissionResourceConstants.cs`
- [X] T004 Marten kaydını doğrula: `src/services/Commission.Api/Program.cs` — `opts.Schema.For<MerchantCommission>();` zaten var (değişiklik gerekmez; yeni index/seed EKLENMEZ). Yalnız doğrula.

**Checkpoint**: Aggregate derlenmiyor olabilir (command/query eski imzaya bağlı) — US1'de düzeltilir. `MerchantCommission.cs` izole doğru.

---

## Phase 3: User Story 1 — Grid ile komisyon yönetimi (Priority: P1) 🎯 MVP

**Goal**: Operatör bir merchant için kombinasyon-bazlı oranları tek grid'de toplu girer/günceller (banka görünürlüğü olmadan da çalışır).

**Independent Test**: Merchant seç → grid tüm kombinasyonları eksik işaretiyle açar → birkaç hücreye oran gir → tek `POST /bulk` ile kaydet → yeniden açınca değerler dolu. `rate <= 0` reddedilir; `(MerchantId, Criteria)` upsert kopya oluşturmaz.

### Backend

- [X] T005 [US1] `CreateMerchantCommission`'ı refactor et: `src/services/Commission.Api/Domains/MerchantCommissions/Features/Commands/CreateMerchantCommission.cs` — komut `(Guid MerchantId, CriteriaDto Criteria, decimal Rate)`; `BankCommission` YÜKLEME; `Criteria.FromCodes` ile kriter kur (hata → messages); `(MerchantId, Criteria)` mevcutsa `UpdateRate`, yoksa `Create`; `session.Store`. `CriteriaDto` bu slice'ta record olarak tanımlanır (contracts).
- [X] T006 [US1] `UpdateMerchantCommission`'ı refactor et: `src/services/Commission.Api/Domains/MerchantCommissions/Features/Commands/UpdateMerchantCommission.cs` — `BankCommission` yüklemeyi KALDIR; `merchantCommission.UpdateRate(cmd.Rate)` (tek argüman); bulunamazsa `NotFound`. `using Commission.Api.Domains.BankCommissions;` gereksizse sil.
- [X] T007 [US1] Yeni `BulkUpsertMerchantCommissions` slice'ı oluştur: `src/services/Commission.Api/Domains/MerchantCommissions/Features/Commands/BulkUpsertMerchantCommissions.cs` — `BulkUpsertBankCommissions` desenini birebir uyarla: komut `(Guid MerchantId, List<Item> Items)`, `Item(CriteriaDto Criteria, decimal Rate)`; `[Transactional]`; mevcut merchant komisyonları belleğe (`MerchantId` + `!IsDeleted`); aynı istekte tekrarlanan kriteri `Dictionary<Criteria,...>` ile izle; `(MerchantId, Criteria)` eşleşme → `UpdateRate` (updated++), yok → `Create` (created++); herhangi hata → messages (atomik geri sarma). Banka/taksit-destek kontrolü YOK (merchant banka-bağımsız). Endpoint `MapPost("/bulk")`, response `{created, updated}`.
- [X] T008 [US1] Endpoint grup metodunu genişlet: `src/services/Commission.Api/Domains/MerchantCommissions/MerchantCommissionEndpointExtension.cs` — zincire `.BulkUpsertMerchantCommissionsGroupItemEndpoint()` ekle.

### Admin UI

- [X] T009 [P] [US1] Bulk request + response modellerini ekle: `src/ui/Admin/Clients/ApiModels.cs` — `BulkUpsertMerchantCommissionsRequest(Guid MerchantId, List<MerchantCommissionBulkItem> Items)`, `MerchantCommissionBulkItem(CriteriaRequest Criteria, decimal Rate)`, `BulkUpsertResult(int Created, int Updated)` (mevcut `CriteriaRequest` yeniden kullanılır).
- [X] T010 [US1] `CommissionApiClient`'a bulk metodu ekle: `src/ui/Admin/Clients/CommissionApiClient.cs` (+ interface) — `BulkUpsertMerchantCommissionsAsync(...)` → `POST /api/v1/merchant-commissions/bulk`.
- [X] T011 [US1] Merchant grid sayfasını oluştur: `src/ui/Admin/Pages/MerchantCommissions/Create.cshtml(.cs)` — `BankCommissions/Create` desenini uyarla; merchant seçimi `IMerchantApiClient.GetAllAsync` ile (id→ad); seçilince kombinasyon matrisi (marka×tip×bölge×taksit) render; boş hücreler `.missing`; **Kaydet** → tek `BulkUpsertMerchantCommissionsAsync`. Decimal binding invariant (mevcut `UseRequestLocalization`).
- [X] T012 [P] [US1] Navigasyona bağlantı ekle: `src/ui/Admin/Pages/Shared/_Layout.cshtml` — "Merchant Komisyon Grid" (`/MerchantCommissions/Create`).

### Test

- [X] T013 [P] [US1] `MerchantCommissionTests.cs`'i yeni imzaya refactor et: `tests/Commission.Api.Tests/MerchantCommissionTests.cs` — `Create(merchantId, criteria, rate)` ve `UpdateRate(rate)` için testler: `rate <= 0` red (`INVALID_RANGE`), `merchantId == Guid.Empty` red, `criteria == null` red, geçerli girdi Ok. Eski banka-bağlı testleri sil.

**Checkpoint**: US1 tek başına çalışır — grid banka görünürlüğü olmadan komisyon toplu girer/günceller. `dotnet build` + `dotnet test` yeşil.

---

## Phase 4: User Story 2 — Oran girerken banka tavanını gör (Priority: P1)

**Goal**: Grid her satırda banka oran aralığını (min–max) gösterir; merchant oranı tavana eşit/altındaysa satır işaretlenir (soft, read-time).

**Independent Test**: Bilinen `BankCommission` oranları varken `GET ?merchantId=` çağır → satırda doğru `bankMin/bankMax`; `rate <= bankMax` → `belowBankCeiling:true`, üstü `false`; banka yoksa `null` + işaretsiz. Banka oranı sonradan değişince işaret tazelenir.

### Backend

- [X] T014 [US2] `GetMerchantCommissions`'ı enriched'e refactor et: `src/services/Commission.Api/Domains/MerchantCommissions/Features/Queries/GetMerchantCommissions.cs` — response item alanları `BankCommissionId/BankCode` KALDIR; ekle `decimal? Rate`, `decimal? BankMin`, `decimal? BankMax`, `bool BelowBankCeiling`, `bool IsMissing`, `Guid? Id`. Handler: merchant komisyonlarını (`MerchantId` + `!IsDeleted`) ve tüm `BankCommission` (`!IsDeleted`) belleğe al; banka oranlarını `Criteria`'ya göre grupla (min/max); satır kümesi = merchant kriterleri ∪ banka-servisli kriterler; her kriter için `rate` (varsa), `bankMin/bankMax` (varsa), `belowBankCeiling = rate != null && bankMax != null && rate <= bankMax`, `isMissing = rate == null`. Data-model.md'ye birebir.

### Admin UI

- [X] T015 [P] [US2] Enriched item modelini güncelle: `src/ui/Admin/Clients/ApiModels.cs` — `MerchantCommissionItem`'a `decimal? Rate, BankMin, BankMax`, `bool BelowBankCeiling, IsMissing`, `Guid? Id` alanlarını yansıt (client aynı JSON'u deserialize eder).
- [X] T016 [US2] Grid'de banka aralığı + tavan-altı görünümü: `src/ui/Admin/Pages/MerchantCommissions/Create.cshtml(.cs)` — her satırda `bankMin–bankMax` kolonu; `belowBankCeiling` → satır kırmızı; `bankMax == null` → "banka yok". (T011 grid'inin üstüne.)
- [X] T017 [P] [US2] Tavan-altı + "banka yok" stilleri: `src/ui/Admin/wwwroot/css/site.css` — `.below-ceiling` (kırmızı) + gerekli yardımcı sınıflar (mevcut `.missing` yeniden kullanılır).

### Test

- [X] T018 [P] [US2] Tavan-altı/aralık hesabı testi: `tests/Commission.Api.Tests/MerchantCommissionTests.cs` (veya yeni `GetMerchantCommissionsProjectionTests.cs`) — saf hesap fonksiyonu üzerinden kenar durumlar: `rate == bankMax` (→ true), `rate > bankMax` (→ false), banka yok (`bankMax == null` → false), çok banka min/max doğru. Hesap saf ise handler'dan ayrık test edilir.

**Checkpoint**: US2 US1 üstüne banka maliyeti görünürlüğü ekler; enriched GET bağımsız doğrulanır.

---

## Phase 5: User Story 3 — Filtrele ve boşları doldur (Priority: P2)

**Goal**: Büyük grid'i eksenlere göre filtrele; görünen boş oran alanlarını toplu doldur; 20'li sayfala.

**Independent Test**: Grid açık → taksit=6 filtrele → yalnız 6 taksitli satırlar → "boşları doldur" bir değer → görünen boş alanlar dolar (dolular korunur) → 20'li sayfalama çalışır.

- [X] T019 [US3] Merchant grid'e eksen filtre + boşları-doldur + 20'li sayfalama bağla: `src/ui/Admin/Pages/MerchantCommissions/Create.cshtml` — 002'de kurulan `wwwroot/js/commission-grid.js` / `filterable-table.js`'i yeniden kullan; toolbar (`data-*`) + eksen filtreleri (marka/tip/bölge/taksit). Backend değişmez. Gerekirse JS'i banka-aralığı/tavan-altı kolonuyla uyumlu genelleştir.
- [X] T020 [P] [US3] Grid eksen seçeneklerini tek kaynaktan al: `src/ui/Admin/Pages/MerchantCommissions/Create.cshtml.cs` — mevcut `GET /bank-commissions/criteria-options` çağrısını kullan (UI enum kopyalamaz). Taksit 1..15.

**Checkpoint**: Tüm hikâyeler tamam; grid üretkenlik katmanıyla tam.

---

## Phase 6: Polish & Cross-Cutting

- [X] T021 [P] Merchant komisyon liste sayfasını enriched kolonlara güncelle: `src/ui/Admin/Pages/MerchantCommissions/Index.cshtml(.cs)` — banka aralığı + tavan-altı işareti (salt-görünüm), yeni item modeliyle.
- [X] T022 [P] Gerekli yeni kullanıcı mesajlarını ekle: `src/ui/Admin/MessageText.cs` (yalnız yeni kod varsa).
- [X] T023 `dotnet build` (0 hata) + `dotnet test` (tüm testler yeşil) doğrula.
- [X] T024 Aspire smoke: `dotnet run --project src/aspire/AppHost/AppHost.csproj` ile Admin'de merchant seç → grid'e oran gir → kaydet → yeniden aç (elle doğrulama; banka HTTP test edilmez).

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2, T002–T004)**: her şeyi bloklar (aggregate imzası).
- **US1 (P3)**: Foundational'a bağlı. MVP — tek başına teslim edilebilir.
- **US2 (P4)**: Foundational'a bağlı; backend (T014) US1'den bağımsız çalışır, ama grid görünümü (T016) US1'in grid sayfasının (T011) üstüne yazar → sıra US1 sonrası.
- **US3 (P5)**: US1 grid sayfasına (T011) bağlı.
- **Polish (P6)**: hikâyeler sonrası.

## Parallel Opportunities

- Foundational: T002 ↔ T003 [P] (farklı dosya).
- US1: T009, T012, T013 [P] birbirinden bağımsız (backend T005–T008 sonrası/paralel farklı dosyalarda).
- US2: T015, T017, T018 [P].
- Polish: T021, T022 [P].

## MVP Scope

**US1 (Phase 3)** tek başına MVP: merchant için kombinasyon-bazlı komisyonların grid ile toplu yönetimi.
US2 banka maliyeti görünürlüğü + soft-flag; US3 üretkenlik. Öneri: US1 → US2 → US3 sırayla, her checkpoint'te build+test.

## Notes

- `[P]` = farklı dosya, bağımsız. Aynı dosyaya dokunan görevler sıralı.
- Yeni proje/paket yok; seed yok; yetki yok (proje geneli erteleme).
- Banka kodu filtresi YOK (FR-017). Merchant.Api'ye backend cross-call YOK (FR-015 UI'da çözülür).