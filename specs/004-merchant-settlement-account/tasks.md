# Tasks: Merchant Settlement Hesabı

**Input**: Design documents from `/specs/004-merchant-settlement-account/`

**Prerequisites**: plan.md, spec.md, data-model.md, contracts/settlement-accounts.http.md, research.md, quickstart.md

**Tests**: Saf domain birim testleri istendi (plan.md + constitution; `tests/Merchant.Api.Tests` mevcut). Handler/HTTP/tenant entegrasyonu birim test edilmez — quickstart senaryolarıyla doğrulanır.

**Organization**: Görevler kullanıcı hikâyesine göre gruplu; her hikâye bağımsız uygulanıp test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, tamamlanmamış göreve bağımlı değil)
- **[Story]**: Görevin ait olduğu hikâye (US1, US2, US3)
- Açıklamalarda tam dosya yolu var

## Path Conventions

Mevcut yapı korunur: backend `src/services/Merchant.Api/`, testler `tests/Merchant.Api.Tests/`.
Yeni proje/servis yok, yeni NuGet paketi yok (CPM korunur). Yeni slice:
`src/services/Merchant.Api/Domains/MerchantSettlementAccounts/`. Mevcut `Merchants` slice'ına dokunulmaz.

**Kod konvansiyonu (her kod üreten göreve DAHİL — ayrı görev değil)**: Her yeni/değişen dosya
(a) Türkçe XML doc / yorum içerir (mevcut `Merchants` slice üslubu; anayasa gereği Türkçe),
(b) kullanılmayan `using` bırakmaz. Bu iki madde her T00x kod görevinin tanımının parçasıdır.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Yeni proje kurulumu yok — mevcut `Merchant.Api`'ye slice eklenir. Yalnız baseline.

- [X] T001 `dotnet build` ile çözümün (PaymentGateway.slnx) temiz derlendiğini doğrula (değişiklik öncesi baseline)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Aggregate + durum enum + yerel banka katalogu + lookup + Marten kaydı + endpoint grup iskeleti. Bu faz bitmeden hiçbir hikâye çalışamaz — tüm command/query bunlara dayanır.

**⚠️ CRITICAL**: Bu faz bitmeden US1/US2/US3 başlayamaz.

- [X] T002 [P] `SettlementAccountStatus` düz enum'unu oluştur: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/SettlementAccountStatus.cs` — `{ Active = 1, Passive = 2 }`; mevcut `MerchantStatus` yorum/konvansiyonunu izle (Türkçe XML doc).
- [X] T003 [P] Yerel `BankCatalog` kopyası: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/Lookups/BankCatalog.cs` — `src/services/Commission.Api/Domains/Banks/BankCatalog.cs` içindeki 4-hane kod+ad listesini birebir kopyala (namespace `Merchant.Api.Domains.MerchantSettlementAccounts.Lookups`); `record CatalogEntry(string Code, string Name)`, `IReadOnlyList<CatalogEntry> All`, `bool TryGetName(string, out string)`. Türkçe XML doc'ta "Commission.Api ile elle senkron; nadir değişir" notu (research D1).
- [X] T004 `IBankCodeLookup` + impl: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/Lookups/BankCodeLookup.cs` — `interface IBankCodeLookup : ISingletonDependency { bool Exists(string code); string? NameOf(string code); }` + `BankCodeLookup` impl (`BankCatalog.All`'dan `Dictionary`, `StringComparer.OrdinalIgnoreCase`); `MccLookup` desenini izle. Scrutor otomatik kaydeder. (T003'e bağlı.)
- [X] T005 `MerchantSettlementAccount` aggregate: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/MerchantSettlementAccount.cs` — `AggregateRoot`'tan türet; private setter alanlar (`MerchantId, BankCode, Iban, AccountOwnerName, AccountNo, AccountDescription, Status`); private ctor. `static ResultDomain<MerchantSettlementAccount> Create(Guid merchantId, string bankCode, string iban, string ownerName, string accountNo, string description)`; `ResultDomain UpdateDetails(string bankCode, string iban, string ownerName, string accountNo, string description)`; `void Activate()` / `void Deactivate()` (Status + `IsActive` + `UpdatedTime`, silme yok). Saf `Validate`: zorunlu alanlar → `COMMON_MESSAGE_VALUE_IS_REQUIRED`; `BankCode` `^\d{4}$` değilse → `COMMON_MESSAGE_INVALID_FORMAT`; IBAN normalize (boşluk temizle + upper) → `^TR\d{24}$` **ve** ISO 13616 mod-97 == 1 değilse → `COMMON_MESSAGE_INVALID_FORMAT`; normalize IBAN saklanır. Varlık/mükerrer kontrolü YOK (handler'da). (T002'ye bağlı.) Bkz. data-model.md.
- [X] T006 Marten document kaydı: `src/services/Merchant.Api/Program.cs` — Marten `AddMarten` opts bloğuna `opts.Schema.For<Merchant.Api.Domains.MerchantSettlementAccounts.MerchantSettlementAccount>();` ekle (mevcut `Merchant` kaydının yanına). Seed/index EKLENMEZ. (T005'e bağlı.)
- [X] T007 Endpoint grup iskeleti: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/MerchantSettlementAccountEndpointExtension.cs` — `public static void AddMerchantSettlementAccountGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)`; `app.MapGroup("api/v{version:apiVersion}/merchants/{merchantId:guid}/settlement-accounts").WithTags("settlement-accounts").WithApiVersionSet(apiVersionSet)` — zincir şimdilik boş (hikâyeler ekler). `Program.cs`'te `app.AddMerchantGroupEndpointExtension(...)` satırından sonra `app.AddMerchantSettlementAccountGroupEndpointExtension(apiVersionSet);` çağır. (T006 ile aynı dosya `Program.cs` — sıralı.)

**Checkpoint**: `dotnet build` yeşil (aggregate + lookup + enum izole derlenir; grup boş zincirle map'li). Hiçbir endpoint henüz iş yapmıyor.

---

## Phase 3: User Story 1 — Merchant'a settlement hesabı ekle (Priority: P1) 🎯 MVP

**Goal**: Operatör geçerli bilgilerle bir merchant'a settlement hesabı ekler; geçersiz IBAN / var olmayan banka veya merchant / mükerrer IBAN reddedilir.

**Independent Test**: Var olan merchant + geçerli TR IBAN + katalog banka kodu → `200` + `id`, hesap `Active`. Bozuk IBAN → `400 INVALID_FORMAT`; katalog dışı bankCode → `400 RECORD_NOT_FOUND`; olmayan merchant → `400 RECORD_NOT_FOUND`; aynı IBAN ikinci kez → `400 RECORD_DUPLICATE`.

### Backend

- [X] T008 [US1] `CreateSettlementAccount` slice'ı: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/Features/Commands/CreateSettlementAccount.cs` — `record CreateSettlementAccountCommand(Guid MerchantId, string BankCode, string Iban, string AccountOwnerName, string AccountNo, string AccountDescription)`; `Response { Guid Id }`; `[Transactional]` handler `(command, IDocumentSession session, IBankCodeLookup bankLookup, CancellationToken ct)`. Akış (CreateMerchant desenini izle): (1) `MerchantSettlementAccount.Create(...)` — başarısızsa `Error(messages)`; (2) merchant var mı: `session.Query<Merchant>().AnyAsync(m => m.Id == cmd.MerchantId && !m.IsDeleted)` yoksa `RECORD_NOT_FOUND` (Property `MerchantId`); (3) `bankLookup.Exists(cmd.BankCode)` değilse `RECORD_NOT_FOUND` (Property `BankCode`); (4) normalize IBAN ile aynı merchant'ta mükerrer: `session.Query<MerchantSettlementAccount>().AnyAsync(a => a.MerchantId == cmd.MerchantId && a.Iban == normalizedIban && !a.IsDeleted)` varsa `RECORD_DUPLICATE` (Property `Iban`); hatalar biriktirilip birlikte döndürülür; (5) `session.Store(result.Data!)`; `Ok({ Id })`. Endpoint `MapPost("/")`, `merchantId` rotadan komuta bağlanır (contracts §1). `Produces` 200/400/500.
- [X] T009 [US1] Endpoint zincirine ekle: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/MerchantSettlementAccountEndpointExtension.cs` — grup zincirine `.CreateSettlementAccountGroupItemEndpoint()` ekle.

### Tests (saf domain)

- [X] T010 [P] [US1] Aggregate birim testleri: `tests/Merchant.Api.Tests/MerchantSettlementAccountTests.cs` — `MerchantTests` stilini izle (xUnit, Türkçe test adları, `CreateValid()` helper). Kapsam: geçerli TR IBAN → Ok + `Active` + normalize IBAN (boşluksuz/upper); bozuk mod-97 → `INVALID_FORMAT`; TR dışı IBAN (`DE...`) → `INVALID_FORMAT`; boşluklu geçerli IBAN → normalize edilip Ok; zorunlu alan boş (merchantId Empty / bankCode / iban / ownerName) → `VALUE_IS_REQUIRED`; bankCode 4-hane değil → `INVALID_FORMAT`.

**Checkpoint**: US1 tek başına test edilebilir — hesap eklenir, tüm doğrulama reddi çalışır. **MVP burada.**

---

## Phase 4: User Story 2 — Hesapları listele ve tekil görüntüle (Priority: P2)

**Goal**: Operatör bir merchant'ın settlement hesaplarını listeler (yalnız o merchant) ve tek hesabın ayrıntısını görür.

**Independent Test**: Bir merchant'a iki hesap ekle → `GET /` yalnız o ikisini döndürür; başka merchant'ın hesabı listede yok. `GET /{accountId}` doğru ayrıntıyı döndürür; başka merchant'ın accountId'si → `404`.

- [X] T011 [P] [US2] `GetMerchantSettlementAccounts` query: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/Features/Queries/GetMerchantSettlementAccounts.cs` — `record Query(Guid MerchantId)`; handler `(query, IDocumentSession session, IBankCodeLookup bankLookup, ct)` → `session.Query<MerchantSettlementAccount>().Where(a => a.MerchantId == query.MerchantId && !a.IsDeleted)`; item `{ Id, BankCode, BankName (bankLookup.NameOf), Iban, AccountOwnerName, Status.ToString() }`. Endpoint `MapGet("/")`, `merchantId` rotadan (contracts §2). GetAllMerchants desenini izle.
- [X] T012 [P] [US2] `GetSettlementAccount` query: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/Features/Queries/GetSettlementAccount.cs` — `record Query(Guid MerchantId, Guid AccountId)`; handler → `Where(a => a.Id == AccountId && a.MerchantId == MerchantId && !a.IsDeleted).FirstOrDefaultAsync`; null → `NotFound()` (tenant sızıntısı yok). Response tam ayrıntı + `BankName`, `AccountNo`, `AccountDescription`, `CreatedTime` (contracts §3). Endpoint `MapGet("/{accountId:guid}")` → `Results.NotFound(result)`. GetMerchant desenini izle.
- [X] T013 [US2] Endpoint zincirine ekle: `MerchantSettlementAccountEndpointExtension.cs` — zincire `.GetMerchantSettlementAccountsGroupItemEndpoint()` ve `.GetSettlementAccountGroupItemEndpoint()` ekle. (T009 ile aynı dosya — sıralı.)

**Checkpoint**: US1 + US2 bağımsız çalışır; tenant izolasyonu (SC-003) doğrulanabilir.

---

## Phase 5: User Story 3 — Güncelle ve durum yönet (Priority: P3)

**Goal**: Operatör hesabı günceller (IBAN/sahip/no/açıklama/banka) ve aktif/pasif yapar; kayıt silinmez.

**Independent Test**: Var olan hesabı geçerli yeni IBAN + sahip ile güncelle → `GET` yeni değerleri döner; bozuk IBAN'la güncelle → `400`, eski değerler korunur. Pasife al → `status="Passive"`, kayıt hâlâ var.

- [X] T014 [US3] `UpdateSettlementAccount` slice'ı: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/Features/Commands/UpdateSettlementAccount.cs` — `record Command(Guid MerchantId, Guid AccountId, string BankCode, string Iban, string AccountOwnerName, string AccountNo, string AccountDescription)`; `[Transactional]` handler `(cmd, IDocumentSession session, IBankCodeLookup bankLookup, ct)`. Akış: hesabı yükle (`Id == AccountId && MerchantId == cmd.MerchantId && !IsDeleted`), yoksa `NotFound`; `bankLookup.Exists` değilse `RECORD_NOT_FOUND`; mükerrer IBAN kontrolü kendisi hariç (`a.Id != AccountId`); `account.UpdateDetails(...)` başarısızsa `Error` (eski değerler korunur — Store çağrılmaz); `session.Store(account)`; `Ok({ Id })`. Endpoint `MapPut("/{accountId:guid}")` (contracts §4).
- [X] T015 [US3] `SetSettlementAccountStatus` slice'ı: `src/services/Merchant.Api/Domains/MerchantSettlementAccounts/Features/Commands/SetSettlementAccountStatus.cs` — `record Command(Guid MerchantId, Guid AccountId, bool IsActive)`; `[Transactional]` handler → hesabı yükle (tenant filtreli), yoksa `NotFound`; `IsActive ? account.Activate() : account.Deactivate()`; `session.Store`; `Ok({ Id, Status.ToString() })`. Endpoint `MapPatch("/{accountId:guid}/status")` (contracts §5).
- [X] T016 [US3] Endpoint zincirine ekle: `MerchantSettlementAccountEndpointExtension.cs` — zincire `.UpdateSettlementAccountGroupItemEndpoint()` ve `.SetSettlementAccountStatusGroupItemEndpoint()` ekle. (Aynı dosya — sıralı.)
- [X] T017 [P] [US3] Aggregate test ekle: `tests/Merchant.Api.Tests/MerchantSettlementAccountTests.cs` — `UpdateDetails` geçerli/bozuk IBAN (bozukta hata + alan değişmez); `Deactivate` → `Status=Passive` + `IsActive=false` + kayıt korunur; `Activate` → `Status=Active` + `IsActive=true`.

**Checkpoint**: Tüm hikâyeler tamam; CRUD + durum uçtan uca.

---

## Phase 6: Polish & Cross-Cutting

- [X] T018 `dotnet build` + `dotnet test tests/Merchant.Api.Tests` yeşil (tüm birim testler geçer)
- [X] T019 Aspire ile ayağa kaldırıp quickstart.md 6 senaryosunu manuel doğrula (`dotnet run --project src/aspire/AppHost/AppHost.csproj`); özellikle tenant izolasyonu (cross-merchant `404`/boş) ve kısmi-kayıt-bırakmama

> **Not**: Türkçe XML doc + kullanılmayan using temizliği ayrı görev DEĞİL — her kod görevine (T002–T017) gömülüdür (bkz. Path Conventions "Kod konvansiyonu").

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002–T007)** → hikâyeler.
- Foundational içi: T002/T003 paralel; T004← T003; T005← T002; T006← T005; T007← T006 (Program.cs sıralı).
- **US1 (T008–T010)**: Foundational sonrası başlar. T010 [P] backend'e paralel.
- **US2 (T011–T013)**: Foundational sonrası başlar; US1'den bağımsız (ayrı dosyalar). T011/T012 paralel; T013 zinciri günceller (US1'in T009'uyla aynı dosya → US1 endpoint eklemesinden sonra).
- **US3 (T014–T017)**: Foundational sonrası başlar; US1/US2'den bağımsız. T017 [P]. T016 zinciri günceller (sıralı, diğer endpoint eklemelerinden sonra).
- **Polish (T018–T019)**: tüm hikâyeler sonrası.

**Not (endpoint extension dosyası)**: T009, T013, T016 aynı `MerchantSettlementAccountEndpointExtension.cs` zincirini düzenler → birbirine göre sıralı (paralel değil). Feature dosyaları (command/query) ayrı → paralel.

## Parallel Opportunities

- Foundational başlangıcı: **T002 + T003** birlikte.
- US1: **T010** (test) backend T008 ile paralel.
- US2: **T011 + T012** birlikte.
- US3: **T017** (test) backend T014/T015 ile paralel.
- Hikâyeler kabaca bağımsız: farklı geliştiriciler US1/US2/US3'ü Foundational sonrası paralel alabilir; tek dikkat noktası ortak endpoint-extension zinciri (merge sırası).

## Implementation Strategy

- **MVP = Phase 1 + 2 + 3 (US1).** Hesap ekleme + tüm doğrulama = payout ön koşulu. Buraya kadar teslim edilebilir.
- Sonra US2 (görünürlük) → US3 (güncelleme/durum) artımlı eklenir.
- Her checkpoint'te `dotnet build` yeşil tutulur; aggregate testleri erken yazılır (IBAN mod-97 kritik).

## Task Summary

- **Toplam**: 19 görev (T001–T019)
- **Setup**: 1 (T001) · **Foundational**: 6 (T002–T007)
- **US1 (P1/MVP)**: 3 (T008–T010) · **US2 (P2)**: 3 (T011–T013) · **US3 (P3)**: 4 (T014–T017)
- **Polish**: 2 (T018–T019)
- **Paralel işaretli [P]**: T002, T003, T010, T011, T012, T017
- **Kod konvansiyonu (Türkçe XML doc + temiz using)**: her kod görevine gömülü (T002–T017)