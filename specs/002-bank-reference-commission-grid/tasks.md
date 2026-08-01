# Tasks: Bank Referansı + Komisyon Grid

**Input**: Design documents from `/specs/002-bank-reference-commission-grid/`

**Prerequisites**: plan.md, spec.md, data-model.md, contracts/ (banks-api.md, bank-commissions-bulk-api.md), research.md, quickstart.md

**Tests**: Saf domain birim testleri istendi (plan.md + tasarım dokümanı). Banka/dış HTTP test edilmez.

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

**Purpose**: Bu dilim mevcut projelere ekleniyor — yeni proje kurulumu yok. Yalnız doğrulama.

- [X] T001 `dotnet build` ile mevcut çözümün (PaymentGateway.slnx) temiz derlendiğini doğrula (baseline)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Hem US1 hem US2'nin dayandığı çekirdek — Bank aggregate, resource kodu, Marten kaydı.

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir kullanıcı hikâyesi başlayamaz.

- [X] T002 [P] `BANK_HAS_COMMISSIONS` sabitini ekle: `src/services/Commission.Api/Domains/SharedKernel/CommissionResourceConstants.cs`
- [X] T003 `Bank` aggregate'ini oluştur: `src/services/Commission.Api/Domains/Banks/Bank.cs` — `AggregateRoot` mirası; private-set `Code` (4 hane, immutable), `Name`, `SupportedInstallments` (`List<int>`, boş değil, 1..15, distinct+artan); sabit `MaxInstallment = 15`; `static ResultDomain<Bank> Create(code, name, installments)`, `ResultDomain Update(name, isActive, installments)`, `void SoftDelete()`, private `NormalizeInstallments` (data-model.md'ye göre; doğrulama kodları `INVALID_FORMAT`/`VALUE_IS_REQUIRED`/`INVALID_RANGE`)
- [X] T004 `Program.cs`'te `Bank`'ı Marten'e kaydet: `src/services/Commission.Api/Program.cs` — `opts.Schema.For<Bank>();` (SEED YOK — FR-001; `IInitialData`/BankSeeder eklenmez)

**Checkpoint**: Bank domain modeli + kalıcılık hazır — US1 ve US2 başlayabilir.

---

## Phase 3: User Story 1 - Banka referansını yönet (Priority: P1) 🎯 MVP

**Goal**: Operatör bankaları merkezi listeden yönetir — ekle/düzenle/sil/listele; kod benzersiz ve immutable; bağlı komisyonu olan banka silinemez.

**Independent Test**: Bankalar sayfası başta boş; banka ekle → listede görünür; aynı kodla tekrar ekle → "zaten var"; adı/taksitleri/aktifliği düzenle → kod değişmez; komisyonsuz banka sil → listeden kalkar; komisyonlu banka sil → engellenir.

### Tests for User Story 1 ⚠️ (önce yaz, FAIL gördükten sonra uygula)

- [X] T005 [P] [US1] `Bank` domain testleri: `tests/Commission.Api.Tests/BankTests.cs` — `Create` geçerli; kötü Code (uzunluk); boş installments; aralık dışı taksit; duplicate → distinct; `Update` Code değişmezliği + geçersiz installments; `SoftDelete` bayrak + `DeletedTime`

### Implementation for User Story 1 (backend)

- [X] T006 [P] [US1] `POST /banks` — `CreateBank.cs`: `src/services/Commission.Api/Domains/Banks/Features/Commands/CreateBank.cs` — aynı `Code` (`!IsDeleted`) varsa `RECORD_DUPLICATE`; `[Transactional]`; yanıt `{ id, code }`
- [X] T007 [P] [US1] `PUT /banks/{code}` — `UpdateBank.cs`: `src/services/Commission.Api/Domains/Banks/Features/Commands/UpdateBank.cs` — Code'a göre yükle (yoksa `RECORD_NOT_FOUND`), `Bank.Update`; `[Transactional]`; yanıt `{ code }`
- [X] T008 [P] [US1] `DELETE /banks/{code}` — `DeleteBank.cs`: `src/services/Commission.Api/Domains/Banks/Features/Commands/DeleteBank.cs` — bağlı `BankCommission` (aynı `BankCode`, `!IsDeleted`) varsa `BANK_HAS_COMMISSIONS`; yoksa `SoftDelete`; `[Transactional]`; yanıt `{ code }`
- [X] T009 [P] [US1] `GET /banks?includeInactive=bool` — `GetBanks.cs`: `src/services/Commission.Api/Domains/Banks/Features/Queries/GetBanks.cs` — `includeInactive` yok/false → yalnız aktif; yanıt `{ items:[...] }`
- [X] T010 [P] [US1] `GET /banks/{code}` — `GetBank.cs`: `src/services/Commission.Api/Domains/Banks/Features/Queries/GetBank.cs` — tek banka; yoksa `RECORD_NOT_FOUND`
- [X] T011 [US1] `BankEndpointExtension.cs`: `src/services/Commission.Api/Domains/Banks/BankEndpointExtension.cs` — grup `api/v{version:apiVersion}/banks`, 5 endpoint'i map et (mevcut `BankCommissionEndpointExtension` pattern'i)
- [X] T012 [US1] `Program.cs`'te banka endpoint grubunu kaydet: `src/services/Commission.Api/Program.cs` — `app.AddBankGroupEndpointExtension(apiVersionSet);` (T004 ile aynı dosya, sıralı)

### Implementation for User Story 1 (Admin UI)

- [X] T013 [P] [US1] Bank modelleri + client metotları: `src/ui/Admin/Clients/ApiModels.cs` (`CreateBankRequest`, `UpdateBankRequest`, `BankListItem`, `BankDetail`, `BanksResponse`) + `src/ui/Admin/Clients/CommissionApiClient.cs` (`CreateBankAsync`, `GetBanksAsync`, `GetBankAsync`, `UpdateBankAsync`, `DeleteBankAsync`) — yeni HttpClient yok, `commission-api` tekrar kullanılır
- [X] T014 [P] [US1] `BANK_HAS_COMMISSIONS` mesaj metni: `src/ui/Admin/MessageText.cs` — "Bankaya bağlı komisyon var, önce onları sil"
- [X] T015 [US1] Bankalar CRUD sayfaları: `src/ui/Admin/Pages/Banks/{Index,Create,Edit,Delete}.cshtml(.cs)` — Index tablo (Code, Name, taksitler, Aktif); Create (Code+Name+taksitler); Edit `{code}` (Name+IsActive+taksitler); Delete `{code}` onay+soft-delete (T013 client'a bağlı)
- [X] T016 [US1] Nav'a "Bankalar" linki: `src/ui/Admin/Pages/Shared/_Layout.cshtml`

**Checkpoint**: US1 tam işlevsel — banka CRUD backend + admin, bağımsız test edilebilir (MVP).

---

## Phase 4: User Story 2 - Bir banka için tüm komisyon kombinasyonlarını doldur (Priority: P1)

**Goal**: Operatör banka seçer → marka×tip×bölge×taksit tam grid'i görür (dolu/eksik işaretli) → eksikleri doldurup tek işlemde kaydeder (bulk upsert).

**Independent Test**: Banka seç → tam kombinasyon grid'i; dolu hücreler oranıyla, boşlar "eksik" işaretli; birkaç boş hücreyi doldur + kaydet → o hücreler dolu görünür; var olan kombinasyona yeni oran → güncellenir (kopya yok).

### Implementation for User Story 2 (backend)

- [X] T017 [US2] `POST /bank-commissions/bulk` — `BulkUpsertBankCommissions.cs`: `src/services/Commission.Api/Domains/BankCommissions/Features/Commands/BulkUpsertBankCommissions.cs` — komut `(string BankCode, List<Item> Items)` / `Item(CriteriaDto Criteria, decimal Rate)`; bankayı Code ile yükle (yoksa/pasif → `RECORD_NOT_FOUND`); `installmentCount` bankanın `SupportedInstallments`'ında değilse `INVALID_RANGE`; her item `(BankCode, Criteria)` var → `UpdateRate`, yok → `Create`; `[Transactional]`; yanıt `{ created, updated }`
- [X] T018 [US2] Bulk endpoint'i mevcut gruba map et: `src/services/Commission.Api/Domains/BankCommissions/BankCommissionEndpointExtension.cs` — tek-tek `POST /` ve `GET /?bankCode` korunur (geriye uyum)

### Implementation for User Story 2 (Admin UI)

- [X] T019 [P] [US2] Bulk modeli + client metodu: `src/ui/Admin/Clients/ApiModels.cs` (`BulkBankCommissionsRequest` + item/criteria) + `src/ui/Admin/Clients/CommissionApiClient.cs` (`BulkUpsertBankCommissionsAsync`)
- [X] T020 [US2] BankCommission grid sayfası: `src/ui/Admin/Pages/BankCommissions/Create.cshtml(.cs)` yeniden — üstte aktif banka dropdown (`GetBanksAsync`); seçim → `GetBankAsync` (taksitler) + `GetBankCommissions?bankCode` (mevcut oranlar); grid = `CardBrand(4)×CardType(3)×TransactionRegion(2)×SupportedInstallments`; her satır oran input'u, boşsa `.missing`; kaydet → doldurulanlar `BulkUpsertBankCommissionsAsync`
- [X] T021 [P] [US2] Grid + `.missing` stili: `src/ui/Admin/wwwroot/css/site.css`

**Checkpoint**: US1 + US2 bağımsız çalışır. Grid ile bir bankanın tüm kombinasyonları tek ekranda doldurulabilir.

---

## Phase 5: User Story 3 - Eksik kapsamı gör ve filtrele (Priority: P3)

**Goal**: Operatör komisyonları banka koduna göre filtreler; yalnız o bankanın kayıtlarına odaklanır.

**Independent Test**: Birden çok bankanın komisyonları varken banka koduna göre filtrele → yalnız o bankanın kayıtları listelenir.

- [X] T022 [US3] BankCommission listesini banka koduna göre filtrele: `src/ui/Admin/Pages/BankCommissions/Index.cshtml(.cs)` — banka dropdown/kod filtresi, `GetBankCommissions?bankCode` (mevcut `GetBankCommissions` query filtre destekliyorsa yalnız UI; desteklemiyorsa `src/services/Commission.Api/Domains/BankCommissions/Features/Queries/GetBankCommissions.cs`'e `bankCode` filtresi ekle)

**Checkpoint**: Üç hikâye de bağımsız işlevsel.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T023 [P] Bulk upsert saf domain testleri (mümkün olan kısımlar — kriter eşleme/upsert kararı): `tests/Commission.Api.Tests/`
- [X] T024 quickstart.md senaryolarını Aspire ile elle doğrula (AppHost başlat → banka ekle → grid doldur → filtrele) (kullanıcı elle doğruladı)
- [X] T025 [P] `dotnet build` + `dotnet test` temiz geçtiğini doğrula

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: bağımsız, hemen başlar
- **Foundational (Phase 2)**: Setup sonrası — TÜM hikâyeleri bloklar
- **US1 (Phase 3)**: Foundational sonrası. Diğer hikâyelere bağımlı değil (MVP)
- **US2 (Phase 4)**: Foundational sonrası. Bank aggregate + `GetBank`/`GetBanks` (T003, T009, T010) hazır olmalı; UI grid US1 client altyapısını (T013) tekrar kullanır
- **US3 (Phase 5)**: Foundational sonrası. Bağımsız test edilebilir
- **Polish (Phase 6)**: istenen hikâyeler bitince

### Within Each User Story

- Testler (T005) önce yazılır, FAIL görülür, sonra uygulanır
- Aggregate (Phase 2) → command/query handler'lar → endpoint extension → Program.cs kaydı
- Backend → Admin client → Admin sayfa
- `Program.cs` görevleri (T004, T012) aynı dosya — sıralı, paralel DEĞİL

### Parallel Opportunities

- T002 ‖ (T003 farklı dosya ama T004 T003'e bağlı)
- US1 backend handler'ları T006–T010 [P] (ayrı dosyalar); T011 hepsini bekler
- US1 Admin T013 ‖ T014 (farklı dosya); T015 T013'e bağlı
- US2: T019 ‖ T021; T020 ikisini + backend'i bekler
- Foundational bitince US1 ve US3 farklı geliştiricilerce paralel yürütülebilir

---

## Parallel Example: User Story 1 backend handler'ları

```bash
Task: "CreateBank.cs — POST /banks"
Task: "UpdateBank.cs — PUT /banks/{code}"
Task: "DeleteBank.cs — DELETE /banks/{code}"
Task: "GetBanks.cs — GET /banks"
Task: "GetBank.cs — GET /banks/{code}"
# Sonra (hepsi bitince): BankEndpointExtension.cs
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational (KRİTİK) → 3. Phase 3 US1 → US1'i bağımsız test et → demo.

### Incremental Delivery

Setup + Foundational → US1 (banka CRUD, MVP) → US2 (grid + bulk) → US3 (filtre). Her hikâye
öncekini bozmadan değer ekler.

---

## Notes

- SEED YOK (FR-001): `Bank` boş başlar; tasarım dokümanındaki `BankSeeder`/`IInitialData` bölümü
  bayat — uygulanmaz.
- Grid eksenleri (CardBrand/CardType/TransactionRegion) domain enum'larından tek kaynaktan gelir:
  yeni `GET /bank-commissions/criteria-options` (`GetCriteriaOptions.cs`) → `Enum.GetNames`. UI
  bunları kopyalamaz; eski `BankCommissions/Create.cshtml` hardcode dropdown'ları grid'e taşındı.
  Yardımcı: `Admin/Pages/Banks/InstallmentText.cs` (taksit metni parse/format).
- CP.VPOS tipleri slice sınırını geçmez; banka listesi runtime bağımlılığı yok.
- Yeni NuGet paketi yok (CPM). Yeni servis/proje yok.
- Yetkilendirme bu dilimde yok (proje geneli erteleme).
- Her görev veya mantıksal grup sonrası commit; checkpoint'lerde hikâyeyi bağımsız doğrula.
---

## Phase 7: Convergence (katalog deltası)

Spec 002 güncellendi: banka adı+kodu kanonik katalogdan seçilir (elle girilmez), taksit 1..15
checkbox grid. Mevcut kod eski "elle Code+Name gir" yaklaşımını uyguluyor; aşağıdaki görevler farkı kapatır.

- [X] T026 [US1] `BankCatalog` statik kanonik katalog: `src/services/Commission.Api/Domains/Banks/BankCatalog.cs` — CP.VPOS `BankService.AllBanks`'ten kopyalanan 47 `(Code, Name)` çifti; `IReadOnlyList<CatalogEntry> All` + `bool TryGetName(string code, out string name)`. CP.VPOS'a runtime bağımlılık yok (değerler elle gömülü) — per FR-001a (missing)
- [X] T027 [US1] `BANK_NOT_IN_CATALOG` sabitini ekle: `src/services/Commission.Api/Domains/SharedKernel/CommissionResourceConstants.cs` — per data-model (missing)
- [X] T028 [US1] `Bank` aggregate katalog uyumu: `src/services/Commission.Api/Domains/Banks/Bank.cs` — `Create(string code, IEnumerable<int> installments)` (Name parametresi kalkar; `BankCatalog.TryGetName` ile Name türet, katalogda yoksa `BANK_NOT_IN_CATALOG`); `Update(bool isActive, IEnumerable<int> installments)` (Name parametresi kalkar, immutable) — per FR-002/FR-003/FR-006 (contradicts)
- [X] T029 [US1] `CreateBank` + `UpdateBank` handler/DTO'ları yeni imzaya uyarla: `src/services/Commission.Api/Domains/Banks/Features/Commands/CreateBank.cs` (komut `{code, supportedInstallments}`, name gövdeden kalkar) ve `UpdateBank.cs` (`UpdateBankRequest{isActive, supportedInstallments}`, name kalkar) — per FR-002/FR-006 (contradicts)
- [X] T030 [US1] `GetBankCatalog` query + endpoint: `src/services/Commission.Api/Domains/Banks/Features/Queries/GetBankCatalog.cs` — `GET /banks/catalog?onlyAvailable=bool`; `onlyAvailable=true` eklenmiş (`!IsDeleted`) bankaları eler; `BankEndpointExtension`'a map et — per FR-001a (missing)
- [X] T031 [US1] Admin client + modeller: `src/ui/Admin/Clients/CommissionApiClient.cs` + `ApiModels.cs` — `GetBankCatalogAsync(bool onlyAvailable)` + `BankCatalogResponse`/`BankCatalogItem`; `CreateBankRequest`→`{Code, SupportedInstallments}`, `UpdateBankRequest`→`{IsActive, SupportedInstallments}` — per FR-001a/FR-002/FR-006 (partial)
- [X] T032 [US1] Admin Create sayfası katalog selectbox + taksit checkbox grid: `src/ui/Admin/Pages/Banks/Create.cshtml(.cs)` — Code+Name text input yerine katalog dropdown (`GetBankCatalogAsync(onlyAvailable:true)`); taksit 1..15 checkbox grid — per US1/AC2 + FR-005 (partial)
- [X] T033 [US1] Admin Edit sayfası salt-görünüm + checkbox grid: `src/ui/Admin/Pages/Banks/Edit.cshtml(.cs)` — Code+Name salt-görünüm (değiştirilemez); taksit 1..15 checkbox grid; IsActive checkbox — per US1/AC4 + FR-005 (partial)
- [X] T034 [US1] Taksit checkbox grid CSS + eski helper temizliği: `src/ui/Admin/wwwroot/css/site.css` (checkbox grid stili) + `src/ui/Admin/Pages/Banks/InstallmentText.cs` kaldır (checkbox `List<int>` bağlaması virgüllü metni değiştirir) — per FR-005 (partial)
- [X] T035 [P] [US1] `BankTests` katalog senaryoları: `tests/Commission.Api.Tests/BankTests.cs` — `Create` katalog kodu (Name katalogdan gelir) / katalog-dışı kod → `BANK_NOT_IN_CATALOG`; `Update` Name+Code immutable; `BankCatalog.TryGetName` var/yok — per quickstart test (partial)

---

## Phase 8: Convergence (grid filtre + toplu doldur)

Spec 002'ye FR-015 (eksen filtresi) + FR-016 (görünen-boş toplu doldur) eklendi. Grid ekranı saf
frontend geliştirmeyle bunları kazanır; backend/model değişmez.

- [X] T036 [US2] Grid client-side JS: `src/ui/Admin/wwwroot/js/commission-grid.js` — (a) marka/tip/bölge/taksit açılır kutularından eksen filtresi: eşleşmeyen `<tr>` gizlenir (AND), "hepsi" sıfırlar; (b) "boşları doldur": girilen oranı yalnız o an GÖRÜNEN + BOŞ rate input'larına yazar, dolu/gizli hücrelere dokunmaz, doldurulan satırın `.missing` görünümünü kaldırır — per FR-015/FR-016 + US2/AC6,AC7 (missing)
- [X] T037 [US2] Grid sayfasına filtre çubuğu + toplu-doldur kutusu + script referansı: `src/ui/Admin/Pages/BankCommissions/Create.cshtml` — grid üstüne 4 filtre `select` (değerler render satırlarından; filtre `data-*` veya hücre metninden), oran input + "Boşları doldur" butonu, `<script src="~/js/commission-grid.js">`; satır/hücrelere JS'in ihtiyacı olan `data-brand/type/region/inst` öznitelikleri — per FR-015/FR-016 (missing)
- [X] T038 [P] [US2] Filtre çubuğu + toplu-doldur stili: `src/ui/Admin/wwwroot/css/site.css` — `.grid-toolbar` (filtre select'leri + doldur kutusu yatay düzen) — per plan touch-point (missing)

---

## Phase 9: Convergence (grid sayfalama + liste banka adı + nav)

Spec 002'ye FR-016 revize (doldur = açık sayfa), FR-017 (liste banka adı), FR-018 (20'li sayfalama)
eklendi. Saf frontend; backend değişmez.

- [X] T039 [US2] Grid 20'li client sayfalama: `src/ui/Admin/wwwroot/js/commission-grid.js` — filtrelenmiş satırlar sayfa başına 20; `Önceki`/`Sonraki` + "Sayfa X / Y"; görünürlük = filtre eşleşir VE geçerli sayfa dilimi; filtre değişince sayfa 1'e döner. Tüm `<input>`'lar DOM'da kalır (Kaydet tümünü kapsar) — per FR-018 + US2/AC8 (missing)
- [X] T040 [US2] "Boşları doldur" açık sayfaya daralt: `src/ui/Admin/wwwroot/js/commission-grid.js` — yalnız o an açık sayfadaki (filtre + sayfa) boş hücreleri doldur — per FR-016 + US2/AC7 (partial)
- [X] T041 [US2] Sayfalama kontrol çubuğu + stil: `src/ui/Admin/Pages/BankCommissions/Create.cshtml` (grid altına Önceki/Sonraki + sayfa göstergesi `id`'leri) + `src/ui/Admin/wwwroot/css/site.css` (`.grid-pager`) — per FR-018 (missing)
- [X] T042 [US3] Komisyon listesinde banka adı: `src/ui/Admin/Pages/BankCommissions/Index.cshtml(.cs)` — "Banka" kolonu `BankCode`→ad (mevcut `GetBanksAsync` listesinden `BankCode`→`Name` eşle, yoksa koda düş); "Komisyon Grid" butonunu kaldır — per FR-017 + US3/AC2 (contradicts)
- [X] T043 [US3] Nav'a "Komisyon Grid" linki: `src/ui/Admin/Pages/Shared/_Layout.cshtml` — grid (Create) erişimi buton yerine üst nav'dan — per plan touch-point (partial)

---

## Phase 10: Convergence (liste filtre+sayfalama + jenerik modül)

Spec 002'ye FR-019 (komisyon listesinde eksen filtresi + 20'li sayfalama) eklendi ve plan JS'i jenerik
`filterable-table.js`'e taşımayı öngörüyor. Saf frontend; backend değişmez.

- [X] T044 [US3] JS refactor jenerik modül: `src/ui/Admin/wwwroot/js/filterable-table.js` (YENİ; `commission-grid.js`'ten) — bir kök `[data-filterable]` altında `[data-filter]` select'leri, `data-*` satırları, `[data-role=prev|next|page-info]` sayfalayıcıyı (20/sayfa) ve OPSIYONEL doldur (`[data-role=fill-rate|fill-empty]`) bağlar; birden çok tabloyu destekler. `commission-grid.js` kaldırılır — per plan: shared module (partial)
- [X] T045 [US2] Grid sayfasını jenerik modüle taşı: `src/ui/Admin/Pages/BankCommissions/Create.cshtml` — grid kökünü `data-filterable` işaretle, mevcut `id` hook'larını `data-role`'e uyarla, script referansı `filterable-table.js`; grid davranışı (filtre + açık-sayfa doldur + 20'li sayfalama) aynen korunur — per plan: shared module (partial)
- [X] T046 [US3] Komisyon listesine filtre + sayfalama: `src/ui/Admin/Pages/BankCommissions/Index.cshtml` — satırlara `data-brand/type/region/inst`; 4 eksen filtre `select` çubuğu (Marka/Tip/Bölge/Taksit; mevcut banka dropdown server-side kalır); alt sayfalayıcı (Önceki/Sonraki + sayfa göstergesi); kök `data-filterable` + `filterable-table.js` (doldursuz, salt-görünüm) — per FR-019 + US3/AC3 (missing)
