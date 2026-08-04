---
description: "Task list — Reference Data BC + Shared Card Taxonomy Kernel"
---

# Tasks: Reference Data BC (Reference.Api) + Shared Card Taxonomy Kernel

**Input**: `/specs/010-reference-data-bc/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Tests**: Saf domain birim testleri dahil (anayasa: host/HTTP entegrasyonu test edilmez; migration eşleme, aggregate invariant, read-model idempotency, lookup davranışı önceliklidir).

**Organization**: User story bazlı fazlar. US1 + US2 ikisi de P1; US2 (enum/migration) US1'den (reference data) bağımsız, paralel ilerleyebilir.

## Format: `[ID] [P?] [Story?] Açıklama + dosya yolu`

---

## Phase 1: Setup (Paylaşılan İskele)

**Amaç**: Yeni projeler + servisler-arası kablolama. Story yok.

- [X] T001 `src/others/SharedKernel/SharedKernel.csproj` oluştur (net10 class library, CPM içinde, minimum bağımlılık) ve `PaymentGateway.slnx`'e ekle
- [X] T002 [P] `src/others/SharedKernel/GlobalUsings.cs` oluştur
- [X] T003 `src/services/Reference.Api/Reference.Api.csproj` oluştur (refs: `Common`, `Shared`, `ServiceDefaults`; paketler: Marten, Marten.Newtonsoft, Newtonsoft.Json, Scrutor, WolverineFx(.Http/.Marten/.Postgresql/.RabbitMQ), Asp.Versioning.Http) ve `PaymentGateway.slnx`'e ekle
- [X] T004 [P] `src/services/Reference.Api/GlobalUsings.cs` + `Dependencies/DependencyExtensions.cs` (Commission Scrutor scan desenini kopyala)
- [X] T005 `src/others/Shared/Utils/Constants/SchemaConstants.cs`'e `ReferenceSchemaName = "referenceManagement"` ekle
- [X] T006 `src/others/Shared/IntegrationEvents.cs`'e `ReferenceDataUpdated(string Kind, IReadOnlyList<ReferenceItem> Items)` + `ReferenceItem(string Code, string Name, string? CountryCode)` kontratlarını ekle
- [X] T007 `src/others/Shared/RabbitMqConstants.cs`'e `ReferenceDataUpdated.Exchange = "reference.data-updated"` ekle
- [X] T008 `src/aspire/AppHost/AppHost.cs`'e `var referenceDb = postgres.AddDatabase("referenceDb")` + `AddProject<Projects.Reference_Api>("reference-api").WithReference(referenceDb).WithReference(rabbit).WaitFor(referenceDb).WaitFor(rabbit)` ekle

---

## Phase 2: Foundational (Reference.Api kaynak-of-truth — US1/US3/US4 için bloklayıcı)

**Amaç**: Reference.Api katalog verisini servis etsin. (US2 buna bağımlı DEĞİL.)

- [X] T009 [P] `src/services/Reference.Api/Domains/Countries/Country.cs` — `AggregateRoot`, `Create(code, name)` + invariant (kod formatı, ad boş değil)
- [X] T010 [P] `src/services/Reference.Api/Domains/Cities/City.cs` — `Create(code, name, countryCode)` + invariant
- [X] T011 [P] `src/services/Reference.Api/Domains/Mccs/Mcc.cs` — `Create(code, name)` + 4-hane invariant (`^\d{4}$`)
- [X] T012 [P] `src/services/Reference.Api/Domains/Banks/Bank.cs` — yalnız code→ad `Create(code, name)` + 4-hane invariant (komisyon-özel öznitelik YOK)
- [X] T013 [P] Embedded JSON seed: `Domains/{Countries,Cities,Mccs,Banks}/Data/*.json` (banks.json = mevcut `BankCatalog` 63 kaydından türet; cities +countryCode) + `Reference.Api.csproj`'a `EmbeddedResource`
- [X] T014 `src/services/Reference.Api/Domains/Seeding/ReferenceSeeder.cs` — `IInitialData`, idempotent (`AnyAsync`), `GetManifestResourceStream` + `JsonConvert`; seed sonrası `ReferenceDataUpdated` yayınla
- [X] T015 `src/services/Reference.Api/Program.cs` — Marten(`referenceManagement`)+Wolverine(Solo)+RabbitMQ `DeclareExchange`(fanout)+`PublishMessage<ReferenceDataUpdated>` + `InitializeWith(new ReferenceSeeder())` + API versioning + endpoint map (Commission Program.cs deseni)
- [~] T016 İPTAL (kullanıcı kararı): Reference.Api'de HİÇ GET/HTTP yüzeyi yok. Tüketici yalnız `ReferenceDataUpdated` event'iyle beslenir.
- [~] T017 İPTAL — bkz T016.
- [~] T018 İPTAL — bkz T016.
- [~] T019 İPTAL — bkz T016.
- [~] T020 İPTAL (snapshot dahil HTTP yok). Bootstrap = `ReferenceStartupPublisher` açılışta tam-set event yayar; taze tüketici durable queue'dan dolar.
- [X] T021 [P] `tests/Reference.Api.Tests/Reference.Api.Tests.csproj` oluştur + aggregate invariant testleri (Country/City/Mcc/Bank `Create` geçerli/geçersiz) ve slnx'e ekle

**Checkpoint**: Reference.Api tek başına ayakta, read API + snapshot çalışır (quickstart S3).

---

## Phase 3: User Story 1 — Tek kaynak + tüketici yerel kopya + Bank konsolidasyonu (P1) 🎯 MVP

**Goal**: Country/City/MCC/Bank kaynak-of-truth Reference.Api; Merchant + Commission yerel read-model'den okur; Merchant onboarding Reference kapalıyken bile çalışır.

**Independent Test**: quickstart S4 (onboarding geçerli/geçersiz + availability) + S5 (Bank konsolidasyon).

- [X] T022 [US1] Merchant read-model dokümanları: `src/services/Merchant.Api/Domains/Reference/ReferenceReadModels.cs` (`ReferenceCountry/City/Mcc/Bank`) + Merchant `Program.cs`'te `Schema.For<>()` kaydı
- [X] T023 [US1] Merchant event handler: `Domains/Reference/ReferenceEventHandler.cs` — `Handle(ReferenceDataUpdated)` idempotent upsert (Code anahtar); Merchant `Program.cs`'te RabbitMQ `DeclareExchange` + durable queue bind (`merchant.reference-sync`)
- [X] T024 [US1] `ILookup` implementasyonlarını read-model'e çevir: `Domains/Merchants/Lookups/Lookups.cs` (`MccLookup/CountryLookup/CityLookup` artık read-model sorgular; arabirim + `BelongsTo` davranışı korunur) + `Domains/SettlementAccounts/Lookups/BankCodeLookup.cs`
- [X] T025 [US1] Merchant gömülü veriyi SİL: `Domains/Merchants/Lookups/{LookupData.cs içi LookupData, LookupRefs.cs}` gömülü Country/City/MCC listeleri + `Domains/SettlementAccounts/Lookups/BankCatalog.cs` (yalnız statik veri; arabirimler kalır)
- [X] T026 [US1] Commission read-model: `src/services/Commission.Api/Domains/Reference/ReferenceBankReadModel.cs` + Marten kaydı + `ReferenceEventHandler` (idempotent upsert) + Commission `Program.cs` durable queue bind (`commission.reference-sync`)
- [X] T027 [US1] Commission `Domains/Banks/Bank.cs` `Create`: banka adını `BankCatalog.TryGetName` yerine read-model lookup'tan türet (enjekte edilen bir `IBankNameLookup`); `SupportedInstallments` Commission'da kalır
- [X] T028 [US1] Commission `Domains/Banks/BankCatalog.cs` (code→ad kopya) SİL
- [~] T029 DEĞİŞTİ: `ILookup` soyutlaması kaldırıldı (kullanıcı kararı) — doğrulama handler'da doğrudan read-model sorgusu (`session.LoadAsync<ReferenceX>(code)`). Upsert idempotency + BelongsTo davranışı DB/handler seviyesinde (anayasa: entegrasyon test edilmez) → quickstart S4/S6 elle. Pure: `ReferenceKeyTests` eklendi.
- [X] T030 [P] [US1] `tests/Commission.Api.Tests` — banka adı türetme read-model'den (Bank.Create) + upsert idempotency testi

**Checkpoint**: US1 tam — MVP teslim edilebilir (Reference serve + iki tüketici senkron + offline dayanıklı + Bank tek kaynak).

---

## Phase 4: User Story 2 — SharedKernel enum tekleştirme + grid migration (P1)

**Goal**: `CardBrand`/`CardType` tek yerde (SharedKernel); Payment+Commission referans verir; Commission grid verisi kanonik int'e kayıpsız remap.

**Independent Test**: quickstart S1 (tek enum tanımı) + S2 (migration idempotent, PREPAID korunur).

- [X] T031 [US2] `src/others/SharedKernel/CardTaxonomy/CardBrand.cs` (kanonik: Unknown=-1, Visa=0..JCB=6) + `CardType.cs` (kanonik superset: Unknown=-1, Debit=0, Credit=1, Prepaid=2)
- [X] T032 [US2] Payment.Api: `Domains/BinCards/{CardBrand.cs, CardType.cs}` SİL, `Reference`... hayır → `Payment.Api.csproj`'a SharedKernel ProjectReference ekle, `using` düzelt (`BinCard.cs`, `BinCardMapping.cs`, `ResolveBinCard.cs`). `CardProgram` Payment'ta kalır. Değerler aynı → veri migration YOK
- [X] T033 [US2] Commission.Api: `Domains/SharedKernel/{CardBrand.cs, CardType.cs}` SİL, `Commission.Api.csproj`'a SharedKernel ProjectReference ekle, `using` düzelt (`Criteria.cs`). `TransactionRegion` Commission'da kalır
- [X] T034 [US2] `Commission.Api/Domains/SharedKernel/Criteria.cs` `FromCodes`: string→enum parse **case-insensitive** (eski `VISA`/`CREDIT` de kabul); `GetCriteriaOptions` kanonik isimleri döner
- [X] T035 [US2] Migration: `Commission.Api/Domains/Migrations/RemapCardTaxonomy.cs` — `BankCommission` + `MerchantCommission` dokümanlarının `Criteria` int'lerini tek geçişte tam sözlükle remap (CardBrand VISA1→0/MASTERCARD2→1/TROY3→2/AMEX4→3; CardType DEBIT2→0/CREDIT1→1/PREPAID3→2), idempotent (migrated işareti / eski-şema koşulu)
- [X] T036 [P] [US2] `tests/Commission.Api.Tests` — remap eşleme testi: her eski değer doğru kanoniğe, PREPAID korunur, iki kez çalışınca idempotent
- [X] T037 [P] [US2] `tests/Commission.Api.Tests` — `Criteria.FromCodes` geriye-uyum (eski `VISA` string'i hâlâ parse eder)

**Checkpoint**: US2 tam — çözümde tek enum tanımı, grid verisi kanonik.

---

## Phase 5: User Story 3 — Katalog büyütme + olay yayılımı (P2)

**Goal**: Referans veri tek yerden büyür, tüketiciye olayla yayılır.

**Independent Test**: quickstart S7 (seed'e şehir ekle → tüketici tanır).

- [X] T038 [US3] Reference.Api diff yayını: `ReferenceSeeder` (T014) seed değişiminde yalnız yeni/değişen kaydı `ReferenceDataUpdated` ile yay (tam-set yerine diff); publish-then-save sırası
- [X] T039 [US3] Tüketici tarafı yeni-kayıt kabulünü doğrula (Merchant read-model yeni şehri upsert eder; ek kod gerekiyorsa T023 handler'ı genelle)

**Checkpoint**: US3 tam — katalog büyütme uçtan uca yayılır.

---

## Phase 6: User Story 4 — Bootstrap (taze tüketici) (P2)

**Goal**: Boş read-model'li taze tüketici açılışta snapshot ile dolar; boş-katalog reddi yok.

**Independent Test**: quickstart S6 (read-model boşalt → restart → ilk istek dolu katalogla).

- [~] T040 DEĞİŞTİ: Snapshot/HTTP iptal (T016-T020). Bootstrap event-only — `Reference.Api/ReferenceStartupPublisher` açılışta katalog tam-setini `ReferenceDataUpdated` ile yayar; taze tüketici durable queue (`{merchant,commission}.reference-sync`) + idempotent upsert ile dolar. HttpClient yok.
- [~] T041 İPTAL — reference-api HTTP yüzeyi yok; typed HttpClient gereksiz (event besleme).

**Checkpoint**: US4 tam — taze instance dayanıklı.

---

## Phase 7: Polish & Cross-Cutting

- [X] T042 [P] `dotnet build PaymentGateway.slnx` 0 hata (yeni projeler + silinen dosyalar sonrası dangling ref temizliği)
- [X] T043 [P] Domain birim testleri yeşil: `dotnet test tests/{Merchant,Commission,Payment,Reference}.Api.Tests`
- [X] T044 [P] Guard grep: `grep -rn "enum CardBrand\|enum CardType" src` yalnız SharedKernel'de; `grep -rn "class BankCatalog" src` boş (SC-001/SC-002)
- [X] T045 quickstart.md senaryo 1–7 Aspire ile elle doğrulama (`dotnet run --project src/aspire/AppHost/AppHost.csproj`)

---

## Dependencies (story tamamlanma sırası)

- **Setup (P1)** → her şeyin önü.
- **Foundational (P2 faz)** → US1, US3, US4 bloklar. **US2 foundational'a bağlı DEĞİL** (SharedKernel bağımsız) — Setup sonrası paralel başlayabilir.
- **US1 (P1)** → MVP. US3/US4 US1'in tüketici altyapısını (read-model + handler) genişletir → US1'den sonra.
- **US2 (P1)** → bağımsız; US1 ile paralel geliştirilebilir (farklı dosyalar/servis yüzeyleri).
- **US3, US4 (P2)** → US1 sonrası.
- **Polish** → en son.

Kritik yol: Setup → Foundational → US1 → (US3, US4). Yan kol: Setup → US2.

## Paralellik

- **Faz içi [P]**: T002/T004 (setup dosyaları); T009–T013 (Reference aggregate'leri + seed, ayrı dosyalar); T016–T019 (read query/endpoint'ler, ayrı Domains); T029/T030, T036/T037 (testler).
- **Fazlar arası**: US2 (Phase 4) tek başına bir geliştirici tarafından US1 (Phase 3) ile eşzamanlı yürütülebilir — farklı dosya kümeleri (SharedKernel + Payment/Commission enum yüzeyi vs Merchant/Commission read-model).

## MVP kapsamı

**Minimum**: Setup + Foundational + **US1** (T001–T030). Bu, tek-kaynak + tüketici yerel kopya + Bank konsolidasyon + offline dayanıklılığı teslim eder — gösterilebilir MVP.
**Sonraki artışlar**: US2 (enum tekleştirme + migration) → US3 (yayılım) → US4 (bootstrap) → Polish.

## Format doğrulaması

Tüm görevler checkbox + Txxx ID + (gerekli yerde) [P]/[US] etiketi + dosya yolu taşır. Setup/Foundational/Polish story etiketsiz; US fazları [US1]/[US2]/[US3]/[US4] etiketli.