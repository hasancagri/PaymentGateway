# Tasks: Merchant OAuth İstemci Düzlemi (G2 — Makine Kimliği)

**Input**: Design documents from `/specs/012-merchant-oauth-client/`

**Prerequisites**: plan.md, spec.md, research.md (D1-D9), data-model.md, contracts/, quickstart.md

**Tests**: Proje konvansiyonu — yalnız saf birim testleri (`MerchantScopeEvaluator`, aggregate geçişleri). Handler/HTTP akışı quickstart canlı senaryolarıyla doğrulanır.

**Organization**: User story bazlı; US1 (token) + US2 (enforcement) birlikte MVP, US3 (yaşam döngüsü) üstüne gelir.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (kontratlar + paketler + Aspire)

- [x] T001 `src/others/Shared/IntegrationEvents.cs`'e `MerchantCreated(Guid MerchantId, string MerchantKey, string Status)` ve `MerchantStatusChanged(Guid MerchantId, string NewStatus)` record'larını ekle (XML doc: yayıncı/tüketici + status string sözleşmesi — contracts/integration-events.md)
- [x] T002 [P] `src/others/Shared/RabbitMqConstants.cs`'e `MerchantLifecycleExchange = "merchant.lifecycle"` ve `IdentityMerchantSyncQueue = "identity.merchant-sync"` sabitlerini ekle
- [x] T003 [P] `src/others/Identity.Server/Identity.Server.csproj`'a sürümsüz `WolverineFx` + `WolverineFx.RabbitMQ` PackageReference'ları ve `Shared` proje referansını ekle (CPM — sürümler `Directory.Packages.props`'ta zaten var; yoksa ekle)
- [x] T004 [P] `src/aspire/AppHost/AppHost.cs`'te `identityServer` resource'una `.WithReference(rabbitmq).WaitFor(rabbitmq)` ekle (mevcut BC kayıtlarındaki desenle aynı)

---

## Phase 2: Foundational (mesajlaşma boru hattı — story'leri bloklar)

- [x] T005 [P] `src/services/Merchant.Api/Program.cs` Wolverine bloğuna `merchant.lifecycle` fanout exchange bildirimi + `MerchantCreated`/`MerchantStatusChanged` için `PublishMessage<T>().ToRabbitExchange(...)` yönlendirmesi ekle (Payment.Api:44-52 deseni)
- [x] T006 [P] `src/others/Identity.Server/Program.cs`'e `UseWolverine` ekle: RabbitMQ transport (Aspire conn-string `rabbitmq`), `merchant.lifecycle` exchange'ine bağlı durable `identity.merchant-sync` kuyruğunu dinle, auto-provision; message store KURMA (research D1 — idempotent tüketim + durable kuyruk yeterli)

**Checkpoint**: Sistem ayağa kalkıyor, exchange/kuyruk RabbitMQ'da görünüyor (henüz olay yok).

---

## Phase 3: User Story 1 — Merchant sistemi kendi kimliğiyle token alır (P1) 🎯 MVP-1

**Goal**: Onboarding otomatik istemci kaydı üretir; merchantId+MerchantKey ile 15 dk ömürlü, `merchant_id` claim'li token alınır.

**Independent Test**: quickstart S1-S2 — onboard → token 200 (`expires_in: 900`, payload'da `merchant_id` + scope dizisi); yanlış secret/bilinmeyen id → 401 `invalid_client`.

- [x] T007 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Commands/CreateMerchant.cs` handler'ının başarı yoluna `IMessageBus.PublishAsync(new MerchantCreated(...))` ekle (status: `merchant.Status.ToString()`)
- [x] T008 [US1] `src/others/Identity.Server/MerchantClientEventHandlers.cs` (YENİ): `MerchantCreated` handler — `IOpenIddictApplicationManager.FindByClientIdAsync` ile idempotent upsert; descriptor: ClientId=MerchantId, ClientSecret=MerchantKey, Confidential, DisplayName=`Merchant {id}`, `Properties["merchant_id"]`, izinler (Token endpoint + ClientCredentials + `scp:merchant.read` + `scp:merchant.write`) yalnız Status=="Active" ise (data-model §1)
- [x] T009 [P] [US1] `src/others/Identity.Server/Connect/TokenEndpoint.cs`: client'ın application kaydından `merchant_id` property'sini oku; varsa access token'a `merchant_id` claim'i ekle (destination: AccessToken; statik istemcilerde property yok → davranış değişmez)
- [x] T010 [P] [US1] `src/others/Identity.Server/Program.cs` OpenIddict server bloğuna `SetAccessTokenLifetime(TimeSpan.FromMinutes(15))` ekle (research D5 — global, admin/agent handler'ları proaktif yenileniyor)

**Checkpoint**: quickstart S1-S2 geçer (canlı doğrulama T023'te toplu).

---

## Phase 4: User Story 2 — Merchant yalnız kendi verisine erişir (P1) 🎯 MVP-2

**Goal**: `MerchantScoped` enforcement: claim-route eşleşmesi; uyuşmazlık/fail-closed → 403; claim'siz token'lar (admin/agent) regresyonsuz.

**Independent Test**: quickstart S3-S5 — kendi kaynağı 200, başkası 403, liste/by-key/create 403, Payment/Commission 401; birim testler `MerchantScopeEvaluator` tablosunu doğrular.

- [x] T011 [P] [US2] `src/others/Common/Utils/Constants/AuthorizationPolicies.cs` (YENİ): `MerchantScoped = "merchant-scoped"`, `AdminPlaneOnly = "admin-plane-only"` sabitleri
- [x] T012 [P] [US2] `src/others/Common/Utils/Authorization/MerchantScopeEvaluator.cs` (YENİ): saf statik `IsAllowed(string? merchantIdClaim, string? routeMerchantId)` — data-model §5 karar tablosu (claim yok→izin; claim var+route yok→ret; eşit→izin; farklı→ret)
- [x] T013 [US2] `src/others/Common/Utils/Authorization/MerchantScopeRequirement.cs` (YENİ): `MerchantScopeRequirement` + `AdminPlaneOnlyRequirement` (IAuthorizationRequirement) ve `IHttpContextAccessor` kullanan iki `AuthorizationHandler` — claim `merchant_id`, route değeri `merchantId`; evaluator'ı çağırır
- [x] T014 [US2] `src/others/Common/Extensions/AuthenticationExtension.cs`: `AddHttpContextAccessor` + iki handler'ın DI kaydı + iki policy'nin `AddAuthorization` bloğuna eklenmesi (scope policy döngüsüne dokunma)
- [x] T015 [US2] `src/services/Merchant.Api/Domains/Merchants/Features/Queries/GetMerchant.cs`: route `{id:guid}` → `{merchantId:guid}` rename + `RequireAuthorization`'a `AuthorizationPolicies.MerchantScoped` ekle
- [x] T016 [P] [US2] `GetAllMerchants.cs`, `GetMerchantByKey.cs`, `CreateMerchant.cs` uçlarına `MerchantScoped` policy ekle (fail-closed 403 — contracts/enforcement.md matrisi)
- [x] T017 [P] [US2] 5 settlement-account ucuna (`CreateSettlementAccount`, `GetSettlementAccounts`, `GetSettlementAccount`, `UpdateSettlementAccount`, `SetSettlementAccountStatus`) `MerchantScoped` policy ekle
- [x] T018 [P] [US2] `tests/Merchant.Api.Tests/MerchantScopeEvaluatorTests.cs` (YENİ): karar tablosunun 4 satırı + boş-string/case kenarları (Common referansı yoksa csproj'a ekle)

**Checkpoint**: Build + testler yeşil; S3-S5 senaryoları geçer. MVP tamam.

---

## Phase 5: User Story 3 — Yaşam döngüsü istemci kaydını yönetir (P2)

**Goal**: Status değişimi olayla Identity'ye akar; Suspended/Passive yeni token alamaz, Active dönüşte alır.

**Independent Test**: quickstart S6 — suspend → token 400 `unauthorized_client` (eldeki token 15 dk yaşar), reactivate → token 200.

- [x] T019 [US3] `src/services/Merchant.Api/Domains/Merchants/Features/Commands/SetMerchantStatus.cs` (YENİ slice): record command (MerchantId + hedef durum), `[Transactional]` handler — aggregate yükle, `Activate()/Deactivate()/Suspend()` çağır (Result deseni, bulunamadı → `COMMON_MESSAGE_RECORD_NOT_FOUND`), başarıda `MerchantStatusChanged` publish; endpoint `PUT merchants/{merchantId}/status` `.RequireAuthorization(AuthorizationScopes.MerchantWrite, AuthorizationPolicies.AdminPlaneOnly)`; `MerchantEndpointExtension.cs`'e kaydet
- [x] T020 [US3] `MerchantClientEventHandlers.cs`'e `MerchantStatusChanged` handler'ı ekle: client yoksa log+NO-OP; varsa NewStatus=="Active" → izinleri geri yaz, değilse izinleri boşalt (secret'a dokunma — research D4)
- [x] T021 [P] [US3] `tests/Merchant.Api.Tests`'te Merchant aggregate `Activate/Deactivate/Suspend` geçiş testleri yoksa ekle (Status + IsActive beklentileri)

**Checkpoint**: S6 geçer; tüm story'ler bağımsız çalışır.

---

## Phase 6: Polish & Doğrulama

- [x] T022 `dotnet build` + `dotnet test tests/Merchant.Api.Tests` + `dotnet test tests/Commission.Api.Tests` — tümü yeşil
- [x] T023 Aspire ile canlı quickstart S1-S7 (`specs/012-merchant-oauth-client/quickstart.md`) — SC-001..SC-005 kanıtı; S7 regresyon (Admin BFF ekranları + 011 S4 agent akışı)
- [x] T024 [P] `.specify/memory/constitution.md` amendment v1.3.0: İlke V `TODO(AUTHZ_MODEL)` G2 kolu KAPANDI (merchant-istemci düzlemi: client_id=merchantId + MerchantKey secret, merchant_id claim, MerchantScoped/AdminPlaneOnly enforcement, status-gated issuance); Sync Impact Report + MINOR bump
- [x] T025 [P] `CLAUDE.md` auth bölümünü güncelle (merchant istemci düzlemi, yeni policy'ler, merchant.lifecycle event akışı) + README'ye 012 satırı

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2 → story fazları**: T003 olmadan T006 derlenmez; T001/T002 olmadan T005/T006 yazılamaz.
- **US1 (Phase 3)**: T005+T006'ya bağlı. İçinde: T007 ile T008 farklı servisler [P-uyumlu]; T009/T010 bağımsız.
- **US2 (Phase 4)**: yalnız Phase 1-2'ye bağlı, US1'den bağımsız (policy'ler claim'siz token'da no-op) — US1 ile paralel yürüyebilir. İçinde: T011+T012 → T013 → T014 → uç beyanları (T015-T017) → T018 her an.
- **US3 (Phase 5)**: T019 endpoint'i T011/T014'teki `AdminPlaneOnly`'ye, T020 T008'deki handler dosyasına bağlı → US1+US2 sonrası önerilir.
- **Polish**: hepsinden sonra.

### Parallel Opportunities

- Phase 1: T002+T003+T004 aynı anda.
- Phase 2: T005+T006 aynı anda (farklı servisler).
- US1 içinde T007/T009/T010 aynı anda; US2 içinde T011+T012, sonra T016+T017+T018 aynı anda.
- US1 ve US2 fazları bütün olarak paralel yürütülebilir.

---

## Implementation Strategy

**MVP = US1 + US2 birlikte** (token almak enforcement'sız yarım, enforcement token'sız test edilemez): Phase 1-4 → S1-S5 doğrula → değer teslim. US3 ikinci artış (S6). Polish'te anayasa amendment'ı unutulmamalı (İlke V governance gereği).