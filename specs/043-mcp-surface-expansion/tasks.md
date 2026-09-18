---

description: "Task list for Merchant + Commission MCP Yüzeyi Genişletme (Merchant.Agent Söküm)"
---

# Tasks: Merchant + Commission MCP Yüzeyi Genişletme (Merchant.Agent Söküm)

**Input**: Design documents from `/specs/043-mcp-surface-expansion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Yalnız plan.md'de açıkça taahhüt edilen tek yeni davranış (Wolverine
`ScopeAuthorizationMiddleware` aktivasyonu) için birim test var (T005) — mevcut aggregate
metotları (`ChangeStatus`/`Approve`/`Reject`) zaten test kapsamında (Domain-TDD gereği yeni test
gerekmiyor); MCP tool ince sarmalayıcıları conventions.md gereği test-sonra/canlı doğrulama
(quickstart.md senaryolarıyla).

**Organization**: Fazlar spec.md'deki user story önceliğine göre (US1→US4); Merchant.Agent+Admin
sayfa sökümü tüm admin tool'lar canlı doğrulandıktan SONRA ayrı bir fazda (research.md #8 sıralaması).

**Durum (implement oturumu)**: Kod TAMAM — build 0 hata + 94 test yeşil (Merchant/Commission/
Payment.Api.Tests) + `check-flow-links.sh` OK. **T011/T018/T019/T022/T028 (canlı quickstart
doğrulama) YAPILAMADI** — bu ortamda Docker daemon çalışmıyor (`docker info` → soket bağlanamadı),
Aspire AppHost Postgres/RabbitMQ container'ları başlatamıyor. Bu beş görev elle (Docker Desktop
açık, Aspire ayakta, Claude desktop `external-admin-agent` bağlı) tamamlanmalı.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Farklı dosyalar, birbirine bağımlı değil — paralel çalışılabilir
- **[Story]**: US1/US2/US3/US4 (spec.md önceliğiyle birebir)

## Path Conventions

Mevcut çoklu-BC .NET çözümü (plan.md "Structure Decision"): `src/services/<Bc>.Api/`,
`src/others/`, `tests/<Bc>.Api.Tests/`. Yeni proje/yeni endpoint YOK.

---

## Phase 1: Setup

**Purpose**: Bu feature'ın TÜM admin tool'larının paylaştığı tool-adı sabitleri.

- [X] T001 [P] `src/others/Shared/McpToolNames.cs` dosyasına `MerchantAdminTools` static class'ı ekle:
      `ActivateMerchant = "admin_activate_merchant"`, `DeactivateMerchant = "admin_deactivate_merchant"`,
      `SuspendMerchant = "admin_suspend_merchant"`, `GetMerchants = "admin_get_merchants"`,
      `GetPendingRegistrations = "admin_get_pending_registrations"`,
      `ApproveRegistration = "admin_approve_registration"`, `RejectRegistration = "admin_reject_registration"`
      sabitleri (contracts/merchant-api-mcp-tools.md ile birebir)
- [X] T002 Aynı dosyaya (T001'in üstüne, aynı dosya — [P] YOK, sıralı) `CommissionAdminTools` static class'ı ekle:
      `GetCommissionPolicy = "admin_get_commission_policy"` (contracts/commission-api-mcp-tools.md)

**Checkpoint**: Tool adı sabitleri hazır — hem Foundational hem tüm user story fazları bunu kullanır.

---

## Phase 2: Foundational (US1/US2/US3'ü bloklar — US4 bağımsız)

**Purpose**: Yeni `merchant.admin` capability scope + tool-bazlı ince yetki zinciri
(research.md #9). Bu faz tamamlanmadan Merchant.Api admin tool'ları (US1/US2/US3) güvenle
implement edilemez — scope kontrolü olmadan yazılırlarsa fail-open kalırlar.

**⚠️ CRITICAL**: US4 (Commission) bu faza bağımlı DEĞİL — mevcut `commission.read` scope'u
yeterli (research.md #9), paralel ilerleyebilir.

- [X] T003 `src/others/Common/Utils/Constants/AuthorizationScopes.cs`'e `merchant.api` bölümüne
      `public const string MerchantAdmin = "merchant.admin";` ekle
- [X] T004 `src/others/Identity.Server/Config.cs`: `ScopeResources` dictionary'sine
      `["merchant.admin"] = "merchant.api"` ekle; `admin-ui` client'ının `Scopes` listesine
      `"merchant.admin"` ekle; `ExternalAdminAgentClientId` (`external-admin-agent`) client'ının
      `Scopes` listesine `"merchant.admin"` ekle. `ecommerce-onboarding` client'ına DOKUNMA
      (ayrım budur — research.md #9)
- [X] T005 `src/services/Merchant.Api/Program.cs`: mevcut `builder.Host.UseWolverine(opts => {...})`
      bloğuna `opts.Policies.AddMiddleware(typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
      chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>()
      is not null);` satırını ekle (mevcut `opts.Policies.UseDurableLocalQueues();` yanına)
- [X] T006 [P] `tests/Merchant.Api.Tests/` altına `ScopeAuthorizationMiddlewareTests.cs` — iki
      senaryo: (a) `[RequiredScope]` taşıyan bir test mesajı + `merchant.admin` claim'i OLMAYAN
      `ClaimsPrincipal` → `UnauthorizedAccessException` fırlatır, (b) claim VARKEN sessizce geçer
      (mevcut `Common.Utils.Authorization.ScopeAuthorizationMiddleware.Before` metodunu doğrudan
      çağırarak, `IHttpContextAccessor` sahte/stub `HttpContext.User` ile)

**Checkpoint**: `merchant.admin` scope'u seed'li, middleware aktif ve test edilmiş — US1/US2/US3
admin tool'ları artık güvenle eklenebilir.

---

## Phase 3: User Story 1 - Merchant durum yönetimi doğal dille (Priority: P1) 🎯 MVP

**Goal**: Operatör Claude desktop'ta "X merchant'ını aktive et/pasife al/askıya al" der; üç ayrı
admin MCP tool'u mevcut `Merchant.ChangeStatus`'u çağırır, statü değişir.

**Independent Test**: quickstart.md Senaryo 2 — `admin_activate_merchant`/`admin_deactivate_merchant`/
`admin_suspend_merchant` sırayla çağrılır, `admin_get_merchants` ile statü doğrulanır; aynı statüye
tekrar çağrıda `changed:false` (hata değil) döner (idempotent no-op, US1 AS2).

### Implementation for User Story 1

- [X] T007 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Agents/Commands/AdminActivateMerchant.cs`
      — `AdminActivateMerchantCommand(Guid MerchantId)` + `[RequiredScope(AuthorizationScopes.MerchantAdmin)]`
      + `[Transactional]` handler (`Merchant.ChangeStatus(MerchantStatus.Active)` çağırır, `{merchantId,
      previousStatus, newStatus, changed}` döner) + `[McpServerToolType]` sarmalayıcı
      (`Name = Shared.MerchantAdminTools.ActivateMerchant`) — contracts/merchant-api-mcp-tools.md
      "admin_activate_merchant" birebir
- [X] T008 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Agents/Commands/AdminDeactivateMerchant.cs`
      — T007 ile birebir aynı desen, hedef `MerchantStatus.Passive`,
      `Name = Shared.MerchantAdminTools.DeactivateMerchant`
- [X] T009 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Agents/Commands/AdminSuspendMerchant.cs`
      — T007 ile birebir aynı desen, hedef `MerchantStatus.Suspended`,
      `Name = Shared.MerchantAdminTools.SuspendMerchant`
- [X] T010 [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Agents/Queries/AdminGetMerchants.cs`
      — `AdminGetMerchantsQuery(string? Status = null)` + `[RequiredScope(AuthorizationScopes.MerchantAdmin)]`
      + handler (`session.Query<Merchant>()`, opsiyonel `Status` filtresi) + `[McpServerToolType]`
      sarmalayıcı (`Name = Shared.MerchantAdminTools.GetMerchants`) — yanıt alanları data-model.md
      "Merchant" tablosundaki ✅ işaretliler, **MerchantKey/SubMerchantKey/Iban HARİÇ** (FR-002)
- [ ] T011 [US1] quickstart.md Senaryo 2'yi canlı çalıştır (Aspire ayakta, Claude desktop
      `external-admin-agent` bağlı) — activate/deactivate/suspend + idempotent no-op + filtreli
      `admin_get_merchants` doğrula

**Checkpoint**: US1 bağımsız çalışır durumda — MVP burada teslim edilebilir.

---

## Phase 4: User Story 2 - Başvuru onay/red yönetimi doğal dille (Priority: P2)

**Goal**: Operatör bekleyen başvuruları listeler, onaylar/reddeder; başvuru alındığında admin'e
mail gider; mükerrer başvuruda anlaşılır mesaj döner.

**Independent Test**: quickstart.md Senaryo 1 — `submit_registration` → Mailpit'te admin maili →
`admin_get_pending_registrations` → `admin_approve_registration` → tekrar onayda
`INVALID_OPERATION_ERROR`; mükerrer `submit_registration`'da yeni kayıt/mail YOK.

### Implementation for User Story 2

- [X] T012 [P] [US2] `src/services/Merchant.Api/Options/AdminNotification.cs` — `AdminEmail: string`
      alanlı Options POCO (data annotations `[Required]`); `Program.cs`'e
      `AddOptions<AdminNotification>().BindConfiguration(nameof(AdminNotification))
      .ValidateDataAnnotations().ValidateOnStart()` + singleton unwrap (mevcut `Onboarding`
      Options'ıyla aynı desen)
- [X] T013 [P] [US2] `src/services/Merchant.Api/Domains/RegisterRequests/Features/Agents/Queries/AdminGetPendingRegistrations.cs`
      — `AdminGetPendingRegistrationsQuery()` + `[RequiredScope(AuthorizationScopes.MerchantAdmin)]`
      + handler (`session.Query<RegisterRequest>().Where(r => r.Status == RegisterRequestStatus.Pending)`)
      + `[McpServerToolType]` sarmalayıcı (`Name = Shared.MerchantAdminTools.GetPendingRegistrations`)
      — yanıt alanları data-model.md "RegisterRequest" tablosundaki ✅ işaretliler
- [X] T014 [US2] `src/services/Merchant.Api/Domains/RegisterRequests/Features/Agents/Commands/AdminApproveRegistration.cs`
      — `AdminApproveRegistrationCommand(Guid RequestId)` + `[RequiredScope(AuthorizationScopes.MerchantAdmin)]`
      + `[Transactional]` handler (`RegisterRequest.Approve(Guid.NewGuid())` → yeni `Merchant`
      `session.Store` — mevcut `ApproveRegisterRequestCommandHandler` iş kuralı birebir kopya) +
      `[McpServerToolType]` sarmalayıcı (`Name = Shared.MerchantAdminTools.ApproveRegistration`)
- [X] T015 [US2] `src/services/Merchant.Api/Domains/RegisterRequests/Features/Agents/Commands/AdminRejectRegistration.cs`
      — `AdminRejectRegistrationCommand(Guid RequestId, string Reason)` + `[RequiredScope
      (AuthorizationScopes.MerchantAdmin)]` + `[Transactional]` handler (`RegisterRequest.Reject(reason)`)
      + `[McpServerToolType]` sarmalayıcı (`Name = Shared.MerchantAdminTools.RejectRegistration`)
- [X] T016 [US2] `src/services/Merchant.Api/Domains/RegisterRequests/Features/Agents/Commands/SubmitRegistration.cs`
      düzenle: `SubmitRegistrationCommandHandler.Handle`'a `IMessageBus bus` parametresi ekle;
      başarılı `session.Store(request)` sonrası (aynı `[Transactional]` handler içinde)
      `await bus.PublishAsync(new Shared.IntegrationEvents.SendEmailRequested(adminNotification.AdminEmail,
      "PG'ye kayıt yaptırmak isteyen var", <requestId/name içeren kısa gövde>))` çağır
      (`AdminNotification` DI'dan enjekte edilir, T012'ye bağımlı) — FR-011
- [X] T017 [US2] Aynı dosyada mükerrer (Pending) dal: `COMMON_MESSAGE_RECORD_DUPLICATE` kodu
      DEĞİŞMEDEN, dönen `MessageItem`/response metnini "talepte bulunmuştunuz, onayı bekleniyor"
      anlamına gelecek şekilde netleştir; bu dalda `SendEmailRequested` publish EDİLMEZ — FR-012
- [ ] T018 [US2] quickstart.md Senaryo 1'i canlı çalıştır (Mailpit dahil) — mail yalnız admin'e
      gider, mükerrer denemede yeni mail/kayıt yok, onay/red + terminal-statü hatası doğrula

**Checkpoint**: US1 + US2 birlikte bağımsız çalışır — onboarding admin akışı MCP'den uçtan uca yürür.

---

## Phase 5: User Story 3 - Merchant/başvuru sorgulama doğal dille (Priority: P3)

**Goal**: `admin_get_merchants` (US1'de zaten yazıldı) + mevcut `registration_status` ile
sorgulama ekransız yürür.

**Independent Test**: quickstart.md Senaryo 2 adım 1 (`admin_get_merchants` ile durum sorgusu) +
mevcut `registration_status` tool'unun DEĞİŞMEDEN çalıştığının doğrulanması.

### Implementation for User Story 3

- [ ] T019 [US3] `src/services/Merchant.Api/Domains/RegisterRequests/Features/Agents/Queries/RegistrationStatus.cs`
      dosyasına DOKUNMA — regresyon-yok kontrolü: mevcut `registration_status` tool'unu (scope
      `merchant.write`, sözleşme aynı) T005'teki yeni middleware wiring'inden SONRA canlı çağırıp
      hâlâ hatasız çalıştığını doğrula (middleware yalnız `[RequiredScope]` TAŞIYAN mesajları
      etkiler — bu tool'da o attribute YOK, davranışı değişmemeli) — FR-009/SC-005 kanıtı

**Checkpoint**: US3 bağımsız doğrulanmış (US1'in `admin_get_merchants`'ına + değişmeyen
`registration_status`'a dayanır, kendi başına yeni kod gerektirmez).

---

## Phase 6: User Story 4 - Komisyon politikası sorgulama doğal dille (Priority: P4)

**Goal**: Commission.Api'nin İLK MCP kurulumu; salt-okuma `admin_get_commission_policy` eklenir,
create/update MCP'ye açılmaz.

**Independent Test**: quickstart.md Senaryo 3 — tanımlı politika için doğru marj/tarife döner,
tanımsız merchant için "tanımlı politika yok".

**Not**: Bu faz Phase 2 (Foundational)'a bağımlı DEĞİL — Merchant.Api'nin scope zincirinden
bağımsız, `commission.read` zaten `ecommerce-onboarding`'de yok (research.md #9). T001/T002
(Phase 1) tamamlandıktan sonra paralel ilerleyebilir.

### Implementation for User Story 4

- [X] T020 [US4] `src/services/Commission.Api/Program.cs`: `builder.Services.AddMcpServer()
      .WithHttpTransport(o => o.Stateless = true).WithToolsFromAssembly();` ekle (Merchant.Api
      Program.cs'teki 029 kurulumuyla birebir) + `app.MapMcp("/mcp")
      .RequireAuthorization(AuthorizationScopes.CommissionRead);` ekle (`app.UseAuthentication()`/
      `UseAuthorization()`'dan SONRA)
- [X] T021 [US4] `src/services/Commission.Api/Domains/CommissionPolicies/Features/Agents/Queries/AdminGetCommissionPolicy.cs`
      — `AdminGetCommissionPolicyQuery(Guid MerchantId)` + handler (mevcut
      `Features/Queries/GetCommissionPolicy.cs`'in Marten sorgusunu bilinçli tekrar eder; politika
      yoksa hata DEĞİL, "tanımlı politika yok" bilgi mesajı) + `[McpServerToolType]` sarmalayıcı
      (`Name = Shared.CommissionAdminTools.GetCommissionPolicy`) — endpoint zaten `CommissionRead`
      istediği için ek `[RequiredScope]` GEREKMEZ (research.md #9)
- [ ] T022 [US4] quickstart.md Senaryo 3'ü canlı çalıştır — tanımlı/tanımsız politika + create/
      update niyetinin MCP'de YAPILMADIĞINI (tool yok) doğrula

**Checkpoint**: Tüm 4 user story bağımsız çalışır durumda.

---

## Phase 7: Söküm — Merchant.Agent + Admin sayfaları (research.md #8 sıralaması)

**Purpose**: Yeni admin tool'lar (Phase 3-6) canlı doğrulandıktan SONRA eski A2A/UI yüzeyi
kaldırılır — geçiş sırasında sistem hiçbir an işlevsiz kalmaz (FR-004/FR-005/FR-006).

**⚠️ CRITICAL**: Bu faz Phase 3 VE Phase 4'ün (US1+US2, T007-T018) TAMAMLANMIŞ ve canlı
doğrulanmış olmasını gerektirir — RegisterRequests/AgentChat ekranlarının yerini alacak MCP
tool'ları hazır olmadan bu sayfalar SİLİNMEZ.

- [X] T023 `src/ui/Admin/Pages/RegisterRequests/` klasörünü tamamen sil (`Index.cshtml`,
      `Index.cshtml.cs`) — FR-006, SC-003
- [X] T024 `src/ui/Admin/Pages/AgentChat/` klasörünü tamamen sil + `src/ui/Admin/Clients/
      MerchantAgentClient.cs` sil — FR-005, SC-004
- [X] T025 `src/ui/Admin/Program.cs`'ten `Merchant.Agent`'a referans/route/DI kaydı (varsa
      `WithReference(merchantAgent)` benzeri) kaldır
- [X] T026 `src/agents/Merchant.Agent/` projesini TAMAMEN sil (`.csproj`, tüm dosyalar,
      `FLOW.md` dahil) — FR-004
- [X] T027 `src/aspire/AppHost/AppHost.cs`'ten Merchant.Agent proje kaydını kaldır;
      `PaymentGateway.slnx`'ten projeyi çıkar
- [ ] T028 quickstart.md Senaryo 4'ü çalıştır — `/AgentChat`+`/RegisterRequests` 404,
      `Merchant.Agent` build/dashboard'da görünmez, `dotnet build`+`dotnet test` 0 hata/regresyon

**Checkpoint**: Söküm tamam — sistemde yalnız MCP admin yüzeyi kaldı.

---

## Phase 8: Polish & Cross-Cutting

**Purpose**: İLKE VII (FLOW.md legibility) — domain süreci belgeleri güncel olmadan feature
TAMAMLANMIŞ sayılmaz (anayasa gereği).

- [X] T029 [P] `src/services/Merchant.Api/FLOW.md` güncelle — yeni admin MCP adımları (statü
      yönetimi + onay/red) sürece eklenir, kenar-anchor'lar yeni sınıf adlarını (`Admin*`) gösterir
- [X] T030 [P] `src/services/Commission.Api/FLOW.md` güncelle — İLK MCP yüzeyi (salt-okuma
      sorgu) sürece eklenir
- [X] T031 [P] `src/others/Identity.Server/FLOW.md` güncelle — yeni `merchant.admin` scope +
      hangi client'lara verildiği (admin-ui, external-admin-agent) not düşülür
- [X] T032 `src/agents/Merchant.Agent/FLOW.md` referansları varsa (CLAUDE.md/README) temizle —
      söküm sonrası ölü referans bırakma
- [X] T033 `scripts/check-flow-links.sh` çalıştır — kenar-anchor'ların koda karşılık geldiğini
      doğrula (rename/silme drifti yakalanmasın)
- [X] T034 `dotnet build` + `dotnet test` tüm çözüm — sıfır hata, sıfır regresyon (SC-005 son kanıt)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Bağımsız, hemen başlar
- **Foundational (Phase 2)**: Phase 1'e bağımlı (tool adı sabitleri); US1/US2/US3'ü BLOKLAR
- **US1 (Phase 3)**: Phase 2 tamamlanınca başlar
- **US2 (Phase 4)**: Phase 2 tamamlanınca başlar (US1'den bağımsız, paralel ilerleyebilir)
- **US3 (Phase 5)**: US1'in T010'una (admin_get_merchants) dayanır — Phase 3'ten SONRA
- **US4 (Phase 6)**: Yalnız Phase 1'e bağımlı — Phase 2'den BAĞIMSIZ, en baştan paralel başlayabilir
- **Söküm (Phase 7)**: US1 (Phase 3) VE US2 (Phase 4) TAMAMLANMIŞ olmalı
- **Polish (Phase 8)**: Tüm önceki fazlar tamamlanınca

### Parallel Fırsatlar

- T001→T002 SIRALI (aynı dosya, `Shared/McpToolNames.cs` — [P] yok, çakışma riski)
- T003/T004 sıralı (T004, T003'teki sabite referans verir) — T005/T006 T004'ten sonra
- T007/T008/T009 tam paralel (üç ayrı dosya, aynı desen)
- T012/T013 paralel; T014/T015 T012'den sonra sıralı yazılabilir (aynı klasör, farklı dosya — aslında paralel de olur, sadece T016 T012'ye bağımlı)
- **US4 (Phase 6) tamamı, US1+US2+US3 (Phase 3-5) ile PARALEL** — farklı BC, farklı ekip/oturum
- T029/T030/T031 paralel (üç farklı FLOW.md)

---

## Implementation Strategy

### MVP First

1. Phase 1 (Setup) + Phase 2 (Foundational) tamamla
2. Phase 3 (US1) tamamla → quickstart Senaryo 2 ile doğrula → **MVP burada** (merchant statü
   yönetimi ekransız çalışıyor)

### Incremental Delivery

1. Setup + Foundational → US1 (MVP) → US2 (onboarding admin akışı tamam) → US3 (sorgulama,
   çoğu zaten US1'den geliyor) → US4 (Commission, paralel ilerlemiş olabilir) → Söküm (Phase 7)
   → Polish (Phase 8)
2. Her checkpoint'te `dotnet build`+`dotnet test` + ilgili quickstart senaryosu çalıştırılır

### Paralel Ekip Stratejisi

- Geliştirici A: Phase 2 (Foundational) → Phase 3 (US1) → Phase 4 (US2)
- Geliştirici B: Phase 1 tamamlanır tamamlanmaz Phase 6 (US4, Commission.Api) — Foundational'ı
  beklemeden başlayabilir
- Phase 7 (Söküm) yalnız A'nın US1+US2'si bitince başlar

---

## Notes

- [P] görevler farklı dosyalar, birbirine bağımlı değil
- Her admin tool kendi dosyasında record+handler+MCP wrapper (VSA "bir feature = bir static
  class", bkz. conventions.md) — Commands/Queries'e IMessageBus ile bile gidilmez (agent slice
  bilinçli tekrar)
- Aggregate metotları (`ChangeStatus`/`Approve`/`Reject`) DOKUNULMAZ — yalnız çağrılır
- Söküm (Phase 7) ASLA Phase 3+4'ten önce başlamaz — geçiş penceresinde sistem çalışır durumda kalır
- FLOW.md güncellemeleri (Phase 8) anayasa İLKE VII gereği ZORUNLU, feature bunlarsız TAMAMLANMIŞ sayılmaz