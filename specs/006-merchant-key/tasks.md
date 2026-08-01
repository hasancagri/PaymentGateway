# Tasks: Merchant Key (gateway kimliği)

**Feature**: 006-merchant-key | **Branch**: `006-merchant-key`
**Input**: [spec.md](./spec.md) · [plan.md](./plan.md) · [research.md](./research.md) · [data-model.md](./data-model.md) · [contracts/merchant-api.md](./contracts/merchant-api.md)

Tek bounded context: **Merchant.Api**. Payment.Api ve diğer BC'ler DEĞİŞMEZ. Yeni paket/dizin yok.
Testler saf domain birim testleri (proje konvansiyonu); handler/HTTP quickstart ile elle doğrulanır.

## Phase 1: Setup

- [X] T001 [P] Doğrula: mevcut yapı ve giriş noktaları — `src/services/Merchant.Api/Domains/Merchants/Merchant.cs` (Create imzası + private-set desen), `Features/Commands/CreateMerchant.cs`, `Features/Queries/GetMerchant.cs` + `GetAllMerchants.cs`, `MerchantEndpointExtension.cs`; `MessageItem.Code` sabitleri `CommonResourceConstants` içinde (`COMMON_MESSAGE_VALUE_IS_REQUIRED`, `COMMON_MESSAGE_RECORD_NOT_FOUND`). Not al, kod değişikliği yok.
- [X] T002 [P] Doğrula: `tests/Merchant.Api.Tests/MerchantTests.cs` mevcut test desenini (saf `Merchant.Create` çağrıları) incele — yeni testler aynı stili izleyecek.

## Phase 2: Foundational (bloklayıcı — user story'lerden önce)

- [X] T003 Yeni saf üretici: `src/services/Merchant.Api/Domains/Merchants/MerchantKeyGenerator.cs` — `public static class MerchantKeyGenerator` + `static string Generate() => "mk_" + Guid.NewGuid().ToString("N");` (URL-güvenli, boşluksuz). Bkz. data-model.md → Domain helper, research.md R3.
- [X] T004 `Merchant` aggregate'ine alan + presence invariant: `src/services/Merchant.Api/Domains/Merchants/Merchant.cs` — `public string MerchantKey { get; private set; } = string.Empty;` ekle; `Create` imzasına **ilk parametre** `string merchantKey` ekle ve atamada set et; `Validate`'in başına `merchantKey` boş/whitespace kontrolü ekle (`Required(nameof(MerchantKey))`). `UpdateProfile` ve status metotları MerchantKey'e DOKUNMAZ. Bkz. data-model.md INV-1/INV-2, research.md R5/R7.

## Phase 3: User Story 1 — Onboarding key üretir (P1) 🎯 MVP

**Goal**: Yeni merchant oluşturulduğunda sistem benzersiz + değişmez merchantKey mint eder; create yanıtı ve tüm okuma sorguları key'i döndürür.

**Independent Test**: Geçerli bilgilerle create → yanıt boş olmayan benzersiz `merchantKey` (`mk_...`); aynı merchant Id ile sorgulanınca aynı key; iki merchant farklı key; istemcinin gönderdiği key yok sayılır.

- [X] T005 [US1] `CreateMerchant` handler: `src/services/Merchant.Api/Domains/Merchants/Features/Commands/CreateMerchant.cs` — format+lookup doğrulamalarından SONRA, `session.Store` ÖNCESİ: `MerchantKeyGenerator.Generate()` ile aday üret, `await session.Query<Merchant>().AnyAsync(m => m.MerchantKey == candidate, ct)` ile benzersizlik denetle, çakışırsa yeniden üret (max ~5 deneme döngüsü). Benzersiz key'i `Merchant.Create(merchantKey, cmd.Name, ...)` ilk parametresi olarak geçir. Bkz. research.md R4.
- [X] T006 [US1] `CreateMerchantResponse`'a alan: aynı dosya (`CreateMerchant.cs`) — `public string MerchantKey { get; set; } = string.Empty;` ekle; başarı dönüşünde `MerchantKey = result.Data!.MerchantKey` doldur.
- [X] T007 [P] [US1] `GetMerchantResponse`'a alan: `src/services/Merchant.Api/Domains/Merchants/Features/Queries/GetMerchant.cs` — response sınıfına `MerchantKey` string ekle; map'te `MerchantKey = merchant.MerchantKey` doldur.
- [X] T008 [P] [US1] `GetAllMerchants` öğe response'una alan: `src/services/Merchant.Api/Domains/Merchants/Features/Queries/GetAllMerchants.cs` — `MerchantItem`'a `MerchantKey` string ekle; `Select`'te `MerchantKey = m.MerchantKey` doldur.
- [X] T009 [P] [US1] Birim testleri: `tests/Merchant.Api.Tests/MerchantTests.cs` — (a) `Create` geçerli merchantKey ile → başarı ve `MerchantKey` set; (b) boş/whitespace merchantKey → `ResultDomain.Error` (presence); (c) `UpdateProfile` ve `Activate/Deactivate/Suspend` sonrası `MerchantKey` değişmez (immutability). Not: benzersizlik/üretim handler'da olduğundan quickstart ile doğrulanır.

**Checkpoint**: US1 tek başına çalışır ve test edilebilir — MVP burada teslim edilebilir.

## Phase 4: User Story 2 — Key ile merchant çöz (P2)

**Goal**: Verilen merchantKey ile merchant çözülür (Id + temel bilgiler + status); yoksa 404.

**Independent Test**: Bilinen key → doğru merchant; var olmayan/boş/biçimsiz key → NotFound (hata değil).

- [X] T010 [US2] Yeni query slice: `src/services/Merchant.Api/Domains/Merchants/Features/Queries/GetMerchantByKey.cs` — `GetMerchantByKeyQuery(string MerchantKey)` + `GetMerchantByKeyResponse` (GetMerchant ile aynı şekil: Id, MerchantKey, temel bilgiler, Status, CreatedTime) + handler: `session.Query<Merchant>().Where(m => m.MerchantKey == query.MerchantKey && !m.IsDeleted).FirstOrDefaultAsync`; null → `FeatureObjectResultModel<...>.NotFound()`. Lookup adları (`ICountryLookup/ICityLookup/IMccLookup`) GetMerchant'taki gibi enjekte edilip ad alanları doldurulur. + endpoint-extension `GetMerchantByKeyGroupItemEndpoint`: `group.MapGet("/by-key/{merchantKey}", ...)` → `IMessageBus.InvokeAsync`, başarı `Ok` / değilse `NotFound`. Bkz. contracts/merchant-api.md §4.
- [X] T011 [US2] Endpoint kaydı: `src/services/Merchant.Api/Domains/Merchants/MerchantEndpointExtension.cs` — zincire `.GetMerchantByKeyGroupItemEndpoint()` ekle.

**Checkpoint**: US2 çalışır; US1'e bağlı (key üretimi olmadan çözecek key yok) ama bağımsız test edilebilir.

## Phase 5: Polish & Doğrulama

- [X] T012 [P] `dotnet build` (tüm çözüm) yeşil — nullable/CPM uyarısı yok.
- [X] T013 [P] `dotnet test tests/Merchant.Api.Tests` yeşil (yeni presence + immutability testleri dahil).
- [X] T014 Aspire ile elle doğrulama: `dotnet run --project src/aspire/AppHost/AppHost.csproj` sonrası quickstart.md Senaryo 1-6 (üretim, aynı key, istemci-key yoksayma, benzersizlik, by-key arama, değişmezlik).

## Dependencies & Execution Order

- **Setup (T001-T002)** → herhangi bir şeyden önce (salt inceleme, paralel).
- **Foundational (T003-T004)** → tüm user story'leri BLOKLAR. T004, T003'ün ürettiği key'i parametre alır; T003 önce/beraber.
- **US1 (T005-T009)**: T005 → T006 (aynı dosya, sıralı). T007/T008/T009 paralel [P] (farklı dosyalar), hepsi T004'e bağlı.
- **US2 (T010-T011)**: T010 → T011 (kayıt slice'a bağlı). US1'e mantıksal bağlı (çözülecek key gerekir) ama kod olarak US1 dosyalarına dokunmaz.
- **Polish (T012-T014)**: tüm implementasyon sonrası. T012/T013 paralel; T014 en son.

## Parallel Opportunities

- T001 ∥ T002 (setup).
- US1 içinde: T007 ∥ T008 ∥ T009 (T005-T006 bittikten sonra; farklı dosyalar).
- T012 ∥ T013 (polish).

## Implementation Strategy

- **MVP = US1** (T001-T009): key üretimi + görünürlük. Tek başına değer teslim eder.
- **Artımlı**: US2 (T010-T011) key-ile-arama ekler. Sonra Polish.
- Ertelenenler (spec Future Considerations): merchant portal teslimi, otomatik bildirim, Payment akışına bağlama, Admin UI gösterimi — bu tasks.md kapsamında DEĞİL.