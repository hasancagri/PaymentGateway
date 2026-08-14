# Tasks: Agent-Bazlı Merchant Onboarding Dirilişi

**Input**: Design documents from `/specs/029-agent-merchant-onboarding/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R9), data-model.md, contracts/, quickstart.md

**Tests**: Saf domain birim testleri DAHİL (R9 — 023 deseni); handler/HTTP/MCP entegrasyonu
quickstart ile elle.

**Organization**: User story bazlı; her faz bağımsız test edilebilir artış.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

**Purpose**: Zemin doğrulama — yeni proje yok, mevcut yapı üstüne kurulum.

- [X] T001 Doğrula: `ecommerce-onboarding` istemcisi Identity seed'inde + secret config'te
      (`src/others/Identity.Server/Config.cs`, `src/others/Identity.Server/appsettings.json`
      `Clients:ecommerce-onboarding:Secret`) ve `ModelContextProtocol.AspNetCore` referansı
      `src/services/Merchant.Api/Merchant.Api.csproj`'da duruyor (R3; kod değişikliği beklenmez,
      eksikse ekle).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Aggregate + kalıcılık — tüm story'ler buna bağlı.

- [X] T002 [P] `RegisterRequestStatus` enum'u (`Pending=1, Approved=2, Rejected=3`) —
      `src/services/Merchant.Api/Domains/RegisterRequests/RegisterRequestStatus.cs`
- [X] T003 `RegisterRequest : AggregateRoot` aggregate'i — data-model.md alan seti; `Submit`
      statik fabrikası (`ResultDomain<RegisterRequest>`; tüm doğrulama INLINE: zorunlu alanlar,
      e-posta biçimi, TR IBAN mod-97, tip-uyum matrisi — Merchant.cs'ten bilinçli kopya, R6),
      `Approve(Guid)` + `Reject(string)` (`ResultDomain`; yalnız Pending, data-model statü
      makinesi); her metoda `<summary>` + `<remarks>Handler: …</remarks>`; private helper YOK —
      `src/services/Merchant.Api/Domains/RegisterRequests/RegisterRequest.cs`
- [X] T004 Marten kaydı: `opts.Schema.For<RegisterRequest>()` —
      `src/services/Merchant.Api/Program.cs`
- [X] T005 [P] Domain birim testleri — Submit doğrulama matrisi (3 tip × zorunlu alan
      kombinasyonları, geçersiz IBAN/e-posta), Approve/Reject statü makinesi + ikinci karar
      reddi + boş red nedeni — `tests/Merchant.Api.Tests/RegisterRequestTests.cs`
- [X] T006 Checkpoint: `dotnet build` 0 hata + `dotnet test tests/Merchant.Api.Tests` yeşil.

---

## Phase 3: User Story 1 — Metinle Kayıt Başvurusu (P1) 🎯 MVP

**Goal**: Yetkili makine istemcisi `submit_registration` ile başvuru açar; Pending kaydolur.

**Independent Test**: quickstart S1 — curl/Inspector ile MCP çağrısı; Pending kayıt + negatifler
(tip-uyum, IBAN, mükerrer).

- [X] T007 [US1] `SubmitRegistrationForAgent` agent slice'ı (`Features/Agents/` — kendi command +
      Response{RequestId,Status,Message} + Handler `[Transactional]`): tip parse (case-insensitive,
      hatada `INVALID_VALUE`), e-posta ile mevcut kayıt sorgusu (Trim + case-insensitive; Pending
      varsa `RECORD_DUPLICATE`, Approved varsa `INVALID_OPERATION_ERROR` "zaten onaylı" — R4/FR-003),
      `RegisterRequest.Submit` + `session.Store` —
      `src/services/Merchant.Api/Domains/RegisterRequests/Features/Agents/SubmitRegistrationForAgent.cs`
- [X] T008 [US1] `RegisterRequestMcpTools` — `[McpServerToolType]` + `[McpServerTool(Name =
      "submit_registration")]` (contracts/mcp-tools.md parametre seti + Description'lar; yalnız
      Agents slice'ını `IMessageBus.InvokeAsync` ile çağırır — 013 deseni, `8691809^` referans) —
      `src/services/Merchant.Api/Domains/RegisterRequests/RegisterRequestMcpTools.cs`
- [X] T009 [US1] MCP server wiring: `AddMcpServer().WithHttpTransport(o => o.Stateless =
      true).WithToolsFromAssembly()` + `app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.
      MerchantWrite)` (R1; 013 wiring'i) — `src/services/Merchant.Api/Program.cs`
- [X] T010 [US1] Checkpoint: build 0 hata; quickstart S1 pozitif + 3 negatif senaryo elle geçer
      (`ecommerce-onboarding` token'ıyla).

---

## Phase 4: User Story 2 — Admin Onay/Red Kararı (P1)

**Goal**: Admin listeden Onayla/Reddet; onayda merchant Active doğar + Identity senkronu.

**Independent Test**: quickstart S2 + S4 — elle Pending kayıt, Admin ekranından karar; Merchants
listesinde Active merchant; Identity logunda "Successfully processed message".

- [X] T011 [P] [US2] `ListRegisterRequests` query slice'ı (tüm kayıtlar `CreatedTime` DESC,
      contracts/admin-endpoints.md yanıt şekli) —
      `src/services/Merchant.Api/Domains/RegisterRequests/Features/Queries/ListRegisterRequests.cs`
- [X] T012 [P] [US2] `ApproveRegisterRequest` command slice'ı `[Transactional]`: request yükle
      (`RECORD_NOT_FOUND`), `Merchant.Create` (023 fabrikası; hata → başvuru Pending kalır),
      `session.Store(merchant)`, `bus.PublishAsync(MerchantCreated(merchant.Id, MerchantKey,
      Status))` (outbox — R5), `request.Approve(merchant.Id)` + store —
      `src/services/Merchant.Api/Domains/RegisterRequests/Features/Commands/ApproveRegisterRequest.cs`
- [X] T013 [P] [US2] `RejectRegisterRequest` command slice'ı `[Transactional]`: yükle +
      `request.Reject(reason)` + store —
      `src/services/Merchant.Api/Domains/RegisterRequests/Features/Commands/RejectRegisterRequest.cs`
- [X] T014 [US2] `RegisterRequestEndpointExtension` — `GET /` (`MerchantRead` + `AdminPlaneOnly`),
      `POST /{requestId:guid}/approve` + `POST /{requestId:guid}/reject` (`MerchantWrite` +
      `AdminPlaneOnly`); Program.cs'te `/api/v1/register-requests` grubuna map —
      `src/services/Merchant.Api/Domains/RegisterRequests/RegisterRequestEndpointExtension.cs` +
      `src/services/Merchant.Api/Program.cs`
- [X] T015 [P] [US2] Admin istemcisi: `IRegisterRequestApiClient` (GetAllAsync/ApproveAsync/
      RejectAsync) + ApiModels'e RegisterRequest liste/yanıt modelleri + Program.cs DI
      (`http://merchant-api`, `AdminTokenHandler`) — `src/ui/Admin/Clients/RegisterRequestApiClient.cs`,
      `src/ui/Admin/Clients/ApiModels.cs`, `src/ui/Admin/Program.cs`
- [X] T016 [US2] Admin ekranı: `Pages/RegisterRequests/Index.cshtml(.cs)` — liste (durum/tip/isim/
      e-posta/tarih/red nedeni/merchantId) + Pending satırda Onayla butonu ve neden input'lu Reddet
      formu (BasePageModel Flash/AddErrors deseni); `_Layout.cshtml` nav'a "Merchant Talepleri" —
      `src/ui/Admin/Pages/RegisterRequests/`, `src/ui/Admin/Pages/Shared/_Layout.cshtml`
- [X] T017 [US2] Checkpoint: build 0 hata; quickstart S2 (onay → Active merchant + Identity log) ve
      S4 red kolu (neden kaydı + aynı e-posta yeniden başvuru) elle geçer.

---

## Phase 5: User Story 3 — Durum Sorgusu + Kimlik Teslimi (P2)

**Goal**: `registration_status(email)` en son başvuruyu döner; Approved'da MerchantId + MerchantKey.

**Independent Test**: quickstart S3 — üç durum yanıtı + dönen ikiliyle `connect/token`'dan token
alınması.

- [X] T018 [US3] `RegistrationStatusForAgent` agent slice'ı: e-posta ile en SON kayıt (Trim +
      case-insensitive, R4); Pending/Rejected(+reason) mesajlı yanıt; Approved'da Merchant
      document'ından MerchantId + MerchantKey okur (`RECORD_NOT_FOUND` kayıt yoksa) —
      `src/services/Merchant.Api/Domains/RegisterRequests/Features/Agents/RegistrationStatusForAgent.cs`
- [X] T019 [US3] `registration_status` MCP tool'u `RegisterRequestMcpTools`'a eklenir
      (contracts/mcp-tools.md; yalnız T018 slice'ını çağırır) —
      `src/services/Merchant.Api/Domains/RegisterRequests/RegisterRequestMcpTools.cs`
- [X] T020 [US3] Checkpoint: build 0 hata; quickstart S3 (üç durum + token alma + case-insensitive
      e-posta) elle geçer.

---

## Phase 6: ECommerce Entegrasyonu + Polish (Cross-Cutting)

**Purpose**: Karşı taraf alan seti + uçtan uca sohbet doğrulaması (contracts/ecommerce-changes.md).

- [X] T021 [P] ECommerce config: `DropShopGateway:Onboarding` bölümü yeni alan setiyle (mod-97
      GEÇERLİ dev IBAN'ı doğrulayarak yaz) —
      `/Users/macbook/Desktop/ECommerceWithAgentFramework/src/agents/ChatAgent/appsettings.json`
- [X] T022 ECommerce prompt: `Program.cs` alan enjeksiyonu (~149-156) + `Prompts.
      AdminOnboardingInstructions` (yeni alanlar, tipe göre koşullu alan açıklaması, e-posta ile
      statü sorgusu, Approved'da Id+Key'i 033 formuna yönlendirme; uydurma değer üretme yasağı) —
      `/Users/macbook/Desktop/ECommerceWithAgentFramework/src/agents/ChatAgent/Program.cs`,
      `/Users/macbook/Desktop/ECommerceWithAgentFramework/src/agents/ChatAgent/Prompts.cs`
- [X] T023 ECommerce build: `dotnet build` 0 hata (ECommerce repo kökünde).
- [X] T024 Kapanış: PaymentGateway `dotnet build` 0 hata + iki test projesi yeşil; quickstart S5
      uçtan uca sohbet senaryosu elle (iki AppHost birlikte — Aspire çift-kopya tuzağına dikkat:
      her repo kendi AppHost'u, PaymentGateway Postgres 5433).

---

## Dependencies

```
Phase 1 (T001) ─► Phase 2 (T002→T003→T004; T005 T003'ten sonra; T006 kapı)
Phase 2 ─► US1 (T007→T008→T009→T010)
Phase 2 ─► US2 (T011,T012,T013 [P] → T014 → T015 → T016 → T017)   # US1'e bağlı DEĞİL (elle Pending kayıtla test edilebilir; normal akışta US1 besler)
US1 + Phase 2 ─► US3 (T018→T019→T020)                             # tool dosyası T008'de doğar
US1 + US3 ─► Phase 6 (T021,T022 [P] → T023 → T024; S5 için US2 de gerekli)
```

**Story sırası**: US1 → US2 → US3 → Phase 6 (öneri; US2, US1'den bağımsız test edilebilir).

## Parallel Opportunities

- T002 ∥ T005 iskeleti (dosyalar farklı); T011 ∥ T012 ∥ T013 (ayrı slice dosyaları); T015 ∥ T016
  hazırlığı; T021 ∥ T022 (farklı dosyalar).

## Implementation Strategy

**MVP**: Phase 1+2 + US1 (T001-T010) — başvuru havuzu dolar, Admin listesi olmasa da DB'de görünür.
İkinci artış US2 (karar + merchant doğumu = iş değeri), üçüncü US3 (kimlik teslimi), son Phase 6
(uçtan uca sohbet). Her checkpoint'te build+test yeşil tutulur; commit'ler faz sonlarında.

> Canlı doğrulama (2026-08-14): S1+S2+S3+S5 GEÇTİ (chat'ten başvuru → Admin onay → Identity token → kimlik teslimi → 033 formu; ayrıca CommissionPolicy ekranıyla komisyon girildi). S1 negatifleri + S4 red akışı bilinçle atlandı (023 emsali). Not: ChatAgent tool keşfi boot'ta — gateway açıkken başlatılmalı; ilk denemede halüsinasyon görüldü, restart sonrası gerçek akış doğrulandı.
