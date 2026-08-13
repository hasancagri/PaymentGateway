# Tasks: Merchant SubMerchant Model

**Input**: Design documents from `/specs/023-merchant-submerchant-model/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/merchants-api.md, quickstart.md

**Tests**: Spec açıkça istiyor (US3 / FR-007 / SC-005) — saf domain birim testleri US3 fazında.

**Organization**: Fazlar user story bazlı; her story bağımsız implement + test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşulabilir (farklı dosya, bekleyen bağımlılık yok)
- **[Story]**: US1 (CRUD), US2 (statü + kimlik zinciri), US3 (testler)

## Path Conventions

Plan yapısı: BC kodu `src/services/Merchant.Api/`, testler `tests/Merchant.Api.Tests/`.
022 malzemesi (`Domains/SubMerchants/`, `Provider/`) ve Identity.Server/Shared DOKUNULMAZ.

---

## Phase 1: Setup

**Purpose**: Branch + aggregate klasör iskeleti

- [X] T001 `023-merchant-submerchant-model` branch'ini master'dan aç (spec klasörü untracked — branch'e taşınır)
- [X] T002 [P] `src/services/Merchant.Api/Domains/Merchants/MerchantStatus.cs` — enum `Active, Passive, Suspended` (data-model.md)
- [X] T003 [P] `src/services/Merchant.Api/Domains/Merchants/MerchantType.cs` — enum `Personal, PrivateCompany, LimitedOrJointStockCompany` (sağlayıcı string'i sızmaz — R2)
- [X] T004 `src/services/Merchant.Api/GlobalUsings.cs` — `Merchant.Api.Domains.Merchants` (+ gerekiyorsa `Common.Results`, `Shared`) global using ekle

---

## Phase 2: Foundational (Blocking)

**Purpose**: Tüm story'lerin bağımlısı olan aggregate

**⚠️ CRITICAL**: US1-US3 bu faz bitmeden başlayamaz

- [X] T005 `src/services/Merchant.Api/Domains/Merchants/Merchant.cs` — `Merchant : AggregateRoot`:
  private setter'lar + non-public default ctor (Marten Newtonsoft ayarıyla uyumlu);
  `static ResultDomain<Merchant> Create(...)` (zorunlu alanlar, e-posta biçimi, TR IBAN
  mod-97, tip-uyum matrisi — hepsi INLINE, private helper YOK — 015; `Id` + `"mk_"+Guid`
  MerchantKey üretimi, `Status=Active`, `SubMerchantKey=null`);
  `ResultDomain UpdateDetails(...)` (aynı doğrulamalar inline TEKRAR — bilinçli;
  Id/MerchantKey/Status/SubMerchantKey değişmez);
  `ResultDomain<bool> ChangeStatus(MerchantStatus)` (aynı statü → `Ok(false)`, farklı →
  `Ok(true)` — R5). Her public metoda `/// <summary>` + `/// <remarks>Handler: ...</remarks>`
  (CreateMerchant.Handler / UpdateMerchant.Handler / ChangeMerchantStatus.Handler).
  Hata kodları `CommonResourceConstants` sabitleriyle `MessageItem` (014). data-model.md matrisi birebir.

**Checkpoint**: Aggregate derlenir — story fazları başlayabilir

---

## Phase 3: User Story 1 - Merchant kaydı oluşturulur ve yönetilir (Priority: P1) 🎯 MVP

**Goal**: Admin düzleminden create/update/get/list CRUD döngüsü; MerchantKey yalnız oluşturma yanıtında.

**Independent Test**: quickstart.md S1-S3 — oluştur → tekil getir → güncelle → listele;
tip-uyum/IBAN/e-posta ihlalleri alan bazlı redded, kayıt üretmez (kimlik zinciri olmadan da test edilir).

- [X] T006 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Commands/CreateMerchant.cs` —
  static class slice: record Command (contracts §1 alanları) + `Response(Guid MerchantId, string MerchantKey)`
  (MerchantKey'in TEK döndüğü yer — SC-004) + `[Transactional]` Handler (`Merchant.Create` çağrısı,
  `IDocumentSession.Store`) + endpoint extension `POST /api/v1/merchants`
  (`RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)`).
  Event yayını BU TASK'TA YOK (T011 ekler — US2).
- [X] T007 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Commands/UpdateMerchant.cs` —
  record Command (route merchantId + contracts §2 gövdesi) + Response (merchant görünümü, MerchantKey YOK) +
  `[Transactional]` Handler (`LoadAsync` → bulunamadı `MessageItem` → `merchant.UpdateDetails(...)`) +
  endpoint `PUT /api/v1/merchants/{merchantId}` (`MerchantWrite` + `AdminPlaneOnly`)
- [X] T008 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Queries/GetMerchant.cs` —
  record Query + Response (contracts §4 — MerchantKey alanı tipte HİÇ YOK) + Handler (`LoadAsync`) +
  endpoint `GET /api/v1/merchants/{merchantId}` (`MerchantRead` + `MerchantScoped` — merchant kendi kaydını okur)
- [X] T009 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Queries/ListMerchants.cs` —
  record Query + Response (tam liste, sayfalama yok — R7) + Handler (`Query<Merchant>()`) +
  endpoint `GET /api/v1/merchants` (`MerchantRead` + `AdminPlaneOnly`)
- [X] T010 [US1] `src/services/Merchant.Api/Program.cs` — 4 endpoint extension'ı apiVersionSet ile map et;
  `dotnet build` 0 hata

**Checkpoint**: US1 MVP — CRUD kodu yerinde, derleme temiz (canlı doğrulama AYRI iş — quickstart.md hazır durur)

---

## Phase 4: User Story 2 - Statü yönetimi ve kimlik zinciri (Priority: P2)

**Goal**: Statü ucu (admin-only) + `merchant.lifecycle` yayınları → OpenIddict istemci senkronu;
token verme statü-kapılı (012 aynen).

**Independent Test**: quickstart.md S4 — oluştur → Identity log'da istemci doğdu → Active token alınır →
Passive yap → token red; statü ucu merchant token'ına 403.

- [X] T011 [US2] `CreateMerchant.cs` Handler'ına atomik yayın ekle:
  `IMessageBus.PublishAsync(new Shared.IntegrationEvents.MerchantCreated(id, merchantKey, MerchantStatus.Active.ToString()))`
  (`[Transactional]` outbox — yalnız commit'te gider; Program.cs yayın kaydı zaten var — R6)
- [X] T012 [US2] `src/services/Merchant.Api/Domains/Merchants/Features/Commands/ChangeMerchantStatus.cs` —
  record Command (route merchantId + `Status` string→enum parse; geçersiz değer `MessageItem`) +
  Response (`MerchantId`, `Status`) + `[Transactional]` Handler (`LoadAsync` → `merchant.ChangeStatus(...)`;
  `.Data! == true` ise `PublishAsync(new MerchantStatusChanged(id, newStatus.ToString()))`, `false` → yayın YOK — R5) +
  endpoint `PUT /api/v1/merchants/{merchantId}/status` (`MerchantWrite` + `AdminPlaneOnly` — US2 senaryo 4)
- [X] T013 [US2] `Program.cs`'e statü endpoint map'i ekle; `dotnet build` 0 hata

**Checkpoint**: US2 kodu yerinde — yayınlar outbox'lu, uçlar policy'li (canlı SC-003 doğrulaması AYRI iş — quickstart S4 hazır durur)

---

## Phase 5: User Story 3 - Domain kuralları test güvencesinde (Priority: P3)

**Goal**: Saf domain birim testleri çözüme döner; `dotnet test` yeşil (DB/ağ yok).

**Independent Test**: `dotnet test` — yalnız aggregate davranışları, dış bağımlılık sıfır.

- [X] T014 [P] [US3] `tests/Merchant.Api.Tests/Merchant.Api.Tests.csproj` — 022'de silinen desen birebir:
  net10.0, `IsTestProject`, sürümsüz PackageReference (xunit, xunit.runner.visualstudio,
  Microsoft.NET.Test.Sdk — CPM'de mevcut, `Directory.Packages.props` DEĞİŞMEZ),
  ProjectReference `../../src/services/Merchant.Api/Merchant.Api.csproj`;
  yanına `GlobalUsings.cs` (`Xunit`, `Common.Results`, `Merchant.Api.Domains.Merchants`)
- [X] T015 [US3] `PaymentGateway.slnx`'e test projesini ekle
- [X] T016 [P] [US3] `tests/Merchant.Api.Tests/MerchantTests.cs` — aggregate davranış testleri:
  (a) üç tip için geçerli Create başarı + `Status=Active` + `MerchantKey` "mk_" öneki;
  (b) tip-uyum matrisi ihlalleri: Personal kimlik-no'suz RED, PrivateCompany kimlik-no/vergi-dairesi/unvan
  eksik RED (TaxNumber'sız GEÇER), LimitedOrJointStock vergi-dairesi/vergi-no/unvan eksik RED,
  Personal vergi alansız GEÇER (spec senaryo 2);
  (c) bozuk IBAN (mod-97) ve bozuk e-posta RED, zorunlu alan boş RED;
  (d) `UpdateDetails`: alanlar değişir, `Id`/`MerchantKey`/`Status`/`SubMerchantKey` değişmez; aynı
  doğrulama redleri;
  (e) `ChangeStatus`: farklı statü `Ok(true)`, aynı statü `Ok(false)` (idempotent), üç statü arası serbest geçiş
- [X] T017 [US3] `dotnet test` koşusu — tümü yeşil; testlerde DB/HTTP/host referansı olmadığını gözden geçir

**Checkpoint**: SC-005 kanıtlı

---

## Phase 6: Polish & Cross-Cutting

- [X] T018 `dotnet build` (tüm çözüm) 0 hata + `dotnet test` yeşil (SC-005; canlı SC-001..SC-004 doğrulaması ayrı iş — quickstart.md rehber olarak hazır)
- [X] T019 `CLAUDE.md` — Merchant.Api bölümünü güncelle: "022 ARA DURUM" notunu yeni gerçeklikle değiştir
  (`Domains/Merchants/` aggregate + 5 slice + test projesi geri geldi; `SubMerchants/` + `Provider/`
  hâlâ hammadde), test satırını güncelle ("Test projesi ŞU AN YOK" → Merchant.Api.Tests var)

---

## Dependencies

```
Phase 1 (T001-T004) ─► Phase 2 (T005) ─► Phase 3 US1 (T006-T010) ─► Phase 4 US2 (T011-T013)
                                    │                                        │
                                    └────────► Phase 5 US3 (T014-T017) ◄─────┘ (yalnız aggregate'e bağlı;
                                                                               US1/US2'den bağımsız koşabilir)
Phase 6 (T018-T019) ─ tüm fazlar sonrası
```

- US1 yalnız T005'e bağlı; US3 de yalnız T005'e bağlı (US1 ile PARALEL gidebilir).
- US2, US1'in `CreateMerchant.cs`'ine dokunur (T011) → US1 sonrası.
- Canlı doğrulama (Aspire + quickstart S1-S4) kapsam DIŞI — kod oturduktan sonra ayrı iş.

## Parallel Examples

- Phase 1: T002 ‖ T003 (ayrı dosyalar)
- Phase 3: T006 ‖ T007 ‖ T008 ‖ T009 (dört ayrı slice dosyası), sonra T010
- Phase 5: T014 ‖ T016 (csproj vs test dosyası), sonra T015, T017
- Story düzeyi: US3 (T014-T017), US1 fazıyla paralel başlayabilir (yalnız T005 bekler)

## Implementation Strategy

- **MVP = Phase 1-3** (US1): CRUD canlı, doğrulamalar çalışır — kimlik zinciri olmadan da değerli.
- Sonra US2 (statü + Identity senkron — 012 düzlemi geri yaşar), ardından US3 testler
  (istenirse US1 ile paralel), en son polish (build + quickstart + CLAUDE.md).
- Her checkpoint'te `dotnet build` 0 hata korunur; commit'ler faz sınırında atılabilir.
