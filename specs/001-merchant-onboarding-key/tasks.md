---
description: "Task list — Merchant.Api + Commission.Api (001-merchant-onboarding-key)"
---

# Tasks: Merchant Onboarding — Merchant.Api + Commission.Api

**Input**: `/specs/001-merchant-onboarding-key/` (plan.md, spec.md, research.md, data-model.md, contracts/)

**Kapsam (bu dilim):** iki yeni Marten + Wolverine BC. **US1** = Merchant registry (key HARİÇ),
**US2** = Banka + Merchant komisyonu. **US3 (seed admin) ve `umk_` key/provision bu dilimde YOK**
(Identity dilimi — Obsidian `DropShop/Yapılacaklar.md`).

**Tests**: Anayasa gereği **saf domain birim testleri** dahil (host/HTTP test edilmez). Slice
başına aggregate invariant testleri.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralel çalışabilir (farklı dosya, bağımlılık yok)
- **[Story]**: US1 / US2 (Setup/Foundational/Polish etiketsiz)
- Referans desen: `src/services/Payment.Api` (Program.cs, vertical slice, EndpointExtension, `[Transactional]`).

---

## Phase 1: Setup (Paylaşılan altyapı)

- [x] T001 `src/services/Merchant.Api/Merchant.Api.csproj` oluştur (net10, Nullable+ImplicitUsings; PackageReference: Marten, Marten.Newtonsoft, Newtonsoft.Json, Scrutor, WolverineFx(+Http/Marten/Postgresql/RabbitMQ/RuntimeCompilation), Asp.Versioning.Http; ProjectReference: `others/Common`, `others/Shared`, `aspire/ServiceDefaults`). `Payment.Api.csproj` şablonundan.
- [x] T002 `src/services/Commission.Api/Commission.Api.csproj` oluştur (aynı şablon; CP.VPOS referansı YOK).
- [x] T003 [P] `src/services/Merchant.Api/GlobalUsings.cs` + `src/services/Commission.Api/GlobalUsings.cs` (Common.Domains, Common.Results, Common.Results.BaseClasses, Common.Utils.Constants, Marten, Wolverine, Wolverine.Attributes, Microsoft.AspNetCore.* — `Payment.Api/GlobalUsings.cs` referans).
- [x] T004 [P] `PaymentGateway.slnx`'e Merchant.Api ve Commission.Api projelerini ekle.
- [x] T005 [P] `tests/Merchant.Api.Tests/Merchant.Api.Tests.csproj` (xUnit; ProjectReference: Merchant.Api).
- [x] T006 [P] `tests/Commission.Api.Tests/Commission.Api.Tests.csproj` (xUnit; ProjectReference: Commission.Api).

---

## Phase 2: Foundational (Bloklayan önkoşullar)

**⚠️ Bu faz bitmeden US1/US2 uçları çalışmaz.**

- [x] T007 `src/services/Merchant.Api/Program.cs`: `AddServiceDefaults` + Marten (`DatabaseSchemaName`, `Connection("merchantDb")`, Newtonsoft non-public setter/ctor) + `IntegrateWithWolverine().ApplyAllDatabaseChangesOnStartup()` + `UseWolverine` (Solo dev, `Discovery.IncludeAssembly`) + `AddApiVersioning` + `AddGlobalExceptionHandler` + `AddAllDependencies` + `MapDefaultEndpoints`. (`Schema.For<Merchant>` T015'te eklenir; endpoint map T019'da.)
- [x] T008 `src/services/Commission.Api/Program.cs`: aynı iskelet, `Connection("commissionDb")`. (`Schema.For<BankCommission>/<MerchantCommission>` T024/T028'de; endpoint map US2'de.)
- [x] T009 [P] `src/services/Merchant.Api/Dependencies/DependencyExtensions.cs` (`AddAllDependencies` Scrutor — `Payment.Api` birebir).
- [x] T010 [P] `src/services/Commission.Api/Dependencies/DependencyExtensions.cs` (aynı).
- [x] T011 `src/aspire/AppHost/AppHost.csproj`'a Merchant.Api + Commission.Api `ProjectReference` ekle.
- [x] T012 `src/aspire/AppHost/AppHost.cs`: `postgres.AddDatabase("merchantDb")` + `"commissionDb")`; `AddProject<Projects.Merchant_Api>("merchant-api")` ve `Commission_Api` — her biri `.WithReference(db).WithReference(rabbit).WaitFor(db).WaitFor(rabbit)`.

**Checkpoint:** İki servis boş ayağa kalkar (`dotnet run --project src/aspire/AppHost/...`).

---

## Phase 3: User Story 1 — Merchant registry (P1) 🎯 MVP

**Goal:** Admin geçerli bilgilerle merchant oluşturur; kayıt registry'de görünür. Key YOK.

**Independent Test:** `POST /api/v1/merchants` geçerli veriyle `200`+id; geçersiz (email/MCC/webhook) `400`; `GET` ile geri okunur (lookup adları çözülür).

### Domain

- [x] T013 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/MerchantStatus.cs` — smart-enum: Active/Passive/Suspended (biçim implement-time; data-model notu).
- [x] T014 [US1] `src/services/Merchant.Api/Domains/Merchants/Merchant.cs` — `Merchant : AggregateRoot`; private setter; `static ResultDomain<Merchant> Create(name,email,phone,countryCode,cityCode,mcc,webhookUrl)` **format** doğrulama (isim/email/telefon boş değil; email format; MCC `^\d{4}$`; webhook mutlak http(s) URL); `UpdateProfile(...)`; `Activate/Deactivate/Suspend`. Mesaj kodları `CommonResourceConstants`.

### Lookup (kod-içi gömülü — DB'de değil)

- [x] T015 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Lookups/LookupRefs.cs` — `record MccRef/CountryRef/CityRef` + gömülü veri (static map veya embedded JSON: en az TR + birkaç il + birkaç MCC ör. "5411").
- [x] T016 [US1] `.../Lookups/Lookups.cs` — `IMccLookup/ICountryLookup/ICityLookup : ISingletonDependency` + implementasyon (`Exists`, `NameOf`, `ICityLookup.BelongsTo`); veriyi bellekte tutar.

### Features (vertical slice)

- [x] T017 [US1] `.../Features/Commands/CreateMerchant.cs` — command+Response+`[Transactional]` handler+EndpointExtension. Handler: lookup **varlık** doğrulaması (`I*Lookup`, City↔Country) → `Merchant.Create` → `session.Store`. Sonuç `FeatureObjectResultModel<>`.
- [x] T018 [P] [US1] `.../Features/Queries/GetMerchant.cs` — id ile; `countryName/cityName/mccName` lookup'tan çözülür; yoksa RECORD_NOT_FOUND.
- [x] T019 [P] [US1] `.../Features/Queries/GetAllMerchants.cs` — liste.
- [x] T020 [US1] `.../Merchants/MerchantEndpointExtension.cs` — `AddMerchantGroupEndpointExtension` (v1 group). `Program.cs`'e `opts.Schema.For<Merchant>()` + endpoint map + `NewApiVersionSet` ekle.

### Tests (saf domain)

- [x] T021 [P] [US1] `tests/Merchant.Api.Tests/MerchantTests.cs` — Create geçerli→Ok; boş email/isim→Error; email format; MCC `^\d{4}$`; webhook URL; Suspend/Deactivate durum geçişleri.

**Checkpoint:** US1 bağımsız çalışır (quickstart Senaryo 1). **MVP burada.**

---

## Phase 4: User Story 2 — Banka + Merchant komisyonu (P2)

**Goal:** Admin banka oranı tanımlar; merchant oranı girer; `merchantRate > bankRate` zorlanır; liste sadece o merchant'ı döner.

**Independent Test:** quickstart Senaryo 2–4 (banka oranı; 2.40>1.75 Ok; 1.75/1.50 red; izolasyon).

### Domain — ortak (Shared)

- [x] T022 [P] [US2] `src/services/Commission.Api/Domains/Shared/CardBrand.cs`, `CardType.cs`, `TransactionRegion.cs` — smart-enum (VISA/MASTERCARD/TROY/AMEX; CREDIT/DEBIT/PREPAID; DOMESTIC/INTERNATIONAL).
- [x] T023 [P] [US2] `.../Domains/Shared/Criteria.cs` — `record Criteria(CardBrand, CardType, TransactionRegion, int InstallmentCount)`; `InstallmentCount>=1`.

### BankCommission

- [x] T024 [US2] `.../Domains/BankCommissions/BankCommission.cs` — `AggregateRoot`; `Create(bankCode,criteria,rate)` (bankCode 4 hane, installment≥1, rate≥0); `UpdateRate(rate)`.
- [x] T025 [US2] `.../Domains/BankCommissions/Features/Commands/CreateBankCommission.cs` — handler `(BankCode,Criteria)` duplicate kontrol (RECORD_DUPLICATE) → Create → Store.
- [x] T026 [P] [US2] `.../Domains/BankCommissions/Features/Queries/GetBankCommissions.cs` — opsiyonel `bankCode` filtresi.
- [x] T027 [US2] `.../Domains/BankCommissions/BankCommissionEndpointExtension.cs` + `Program.cs` `opts.Schema.For<BankCommission>()` + map.

### MerchantCommission

- [x] T028 [US2] `.../Domains/MerchantCommissions/MerchantCommission.cs` — `AggregateRoot`; `Create(merchantId, bankCommission, rate)` **invariant `rate > bankCommission.Rate`** (kesin büyük; kod `MERCHANT_RATE_MUST_EXCEED_BANK_RATE`); Criteria/BankCode snapshot; `UpdateRate(rate, bankCommission)`.
- [x] T029 [US2] `.../Domains/MerchantCommissions/Features/Commands/CreateMerchantCommission.cs` — handler: `BankCommissionId` yükle (yoksa RECORD_NOT_FOUND); `(MerchantId,BankCommissionId)` varsa **upsert→UpdateRate**, yoksa Create. `merchantId` yalnız Guid (Merchant.Api'ye çağrı YOK).
- [x] T030 [P] [US2] `.../Features/Commands/UpdateMerchantCommission.cs` — id ile; aynı invariant.
- [x] T031 [P] [US2] `.../Features/Queries/GetMerchantCommissions.cs` — `Where(c => c.MerchantId == merchantId)` düz filtre (tenant ertelendi).
- [x] T032 [US2] `.../MerchantCommissions/MerchantCommissionEndpointExtension.cs` + `Program.cs` `opts.Schema.For<MerchantCommission>()` + map.

### Tests (saf domain)

- [x] T033 [P] [US2] `tests/Commission.Api.Tests/BankCommissionTests.cs` — bankCode 4 hane; rate≥0.
- [x] T034 [P] [US2] `tests/Commission.Api.Tests/MerchantCommissionTests.cs` — `rate>bankRate`→Ok; `rate==bankRate`→Error; `rate<bankRate`→Error; `UpdateRate` aynı invariant; snapshot doğru.

**Checkpoint:** US1 + US2 bağımsız çalışır.

---

## Phase 5: Polish & Cross-Cutting

- [x] T035 `dotnet build` (tüm çözüm) temiz; `dotnet test tests/Merchant.Api.Tests tests/Commission.Api.Tests` yeşil.
- [x] T036 quickstart.md Senaryo 1–4 elle koştur (AppHost açık) — beklenenlerle eşleşme. (S1 valid+GET+red, S2 dup, S3a/b/c invariant+upsert, S4 izolasyon — hepsi geçti. Not: S1 invalid tek mesaj döner (Email); `Merchant.Validate` ilk hatada durur — tasarım gereği, quickstart üçlü-mesaj metni iyimser.)
- [x] T037 [P] Kısa README/not: iki BC + ertelenenler (key/provision, tenant, lookup→DB terfi) → tasarım dokümanına/Obsidian bağ.

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2)** → US1 (P3) / US2 (P4) → **Polish (P5)**.
- US1 ve US2 **farklı servisler** → Foundational sonrası **paralel** çalışılabilir (bağımsız).
- Servis içi: enum/VO → aggregate → features/endpoint → tests.
- T014 (Merchant) T017/T018/T019/T020'yi bloklar. T024 (BankCommission) T028'i (invariant referansı) bloklar; T028 T029'u bloklar.

## Parallel Opportunities

- Setup: T003/T004/T005/T006 [P].
- Foundational: T009/T010 [P] (T011/T012 AppHost sıralı).
- US1 içi: T013/T015 [P]; T018/T019/T021 [P].
- US2 içi: T022/T023 [P]; T026/T030/T031/T033/T034 [P].
- **Servisler arası:** tüm US1 ↔ tüm US2 paralel (ayrı BC, ayrı DB, cross-call yok).

## Implementation Strategy

1. Setup + Foundational → iki servis boş kalkar.
2. **US1 (Merchant.Api)** → quickstart Senaryo 1 → **MVP**.
3. **US2 (Commission.Api)** → Senaryo 2–4 (invariant + izolasyon).
4. Polish → build/test/quickstart.

> **Ertelendi (bu dilim değil):** `umk_` key üretimi/hash saklama + provision, seed admin (US3),
> scope enforcement (`merchants.manage`/`commissions.manage`), Marten conjoined tenant,
> BankCommission↔PosAccount uzlaştırma, `IpList`. Hepsi Obsidian `DropShop/Yapılacaklar.md`'de.