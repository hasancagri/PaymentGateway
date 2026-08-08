---
description: "Task list — Merchant Onboarding (013)"
---

# Tasks: Merchant Onboarding — Agentic Kayıt + İnsan Onayı + Kademeli Yetki

**Input**: `/specs/013-merchant-onboarding/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Tests**: Proje konvansiyonu = **saf domain birim testleri** (`tests/Merchant.Api.Tests`,
`tests/Commission.Api.Tests`). Handler/HTTP/A2A/LLM/Razor entegrasyonu birim test EDİLMEZ —
quickstart ile elle. Aşağıdaki test görevleri yalnız saf aggregate/VO invariant'larını kapsar.

**Organization**: Görevler user story bazında; her story bağımsız uygulanıp doğrulanabilir.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

- **[P]**: Paralel koşabilir (farklı dosya, bağımlılık yok)
- **[Story]**: US1..US6 (spec.md)
- Yollar plan.md yapısına göre (çok-projeli mikroservis çözüm).

---

## Phase 1: Setup (Ortak Altyapı)

**Purpose**: Yeni projeler + paket + AppHost iskeleti

- [X] T001 Merchant.Agent projesini oluştur (`src/agents/Merchant.Agent/`) — Payment.Agent şablonu kopyası (Program.cs, csproj, GlobalUsings); `PaymentGateway.slnx`'e ekle
- [X] T002 [P] Mail.Mcp projesini oluştur (`src/others/Mail.Mcp/`) — web SDK, `ModelContextProtocol.AspNetCore`; slnx'e ekle
- [X] T003 [P] Excel.Mcp projesini oluştur (`src/others/Excel.Mcp/`) — web SDK + ClosedXML; slnx'e ekle; `ClosedXML` sürümünü `Directory.Packages.props`'a ekle
- [X] T004 AppHost: Merchant.Agent + Mail.Mcp + Excel.Mcp servislerini + Mailpit container'ını (SMTP :1025 / UI :8025 catch-all) + simüle aday site resource'unu kaydet (`src/aspire/AppHost/AppHost.cs`)
- [X] T005 [P] Konfig: Mail.Mcp SMTP (dev=Mailpit), Merchant.Agent `OpenAI:ApiKey` (user-secrets), MCP base URL'leri (`appsettings*.json` ilgili projeler)

**Checkpoint**: Yeni projeler derleniyor, AppHost ayağa kalkıyor.

---

## Phase 2: Foundational (Bloklayıcı Ön Koşullar)

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz.

- [X] T006 [P] Yeni integration event'ler: `MerchantProvisioned(MerchantId, MerchantKey, Status)` + `MerchantCommissionGridReady(MerchantId)` (`src/others/Shared/IntegrationEvents.cs`)
- [X] T007 [P] `merchant.commission` fanout exchange + Merchant.Api durable queue sabitleri (`src/others/Shared/RabbitMqConstants.cs`)
- [X] T008 Common `IMailSender` arayüzü + `MailAttachment` VO (`src/others/Common/Mail/IMailSender.cs`)
- [X] T009 Common `MailMcpClient` — Mail.Mcp `/mcp`'ye MCP client + `mail.send` token'lı DelegatingHandler (−30sn yenileme, AgentTokenHandler deseni), marker DI (`src/others/Common/Mail/MailMcpClient.cs`) — depends T008
- [X] T010 Mail.Mcp `send_email` tool (to/subject/body/isHtml/attachments) `System.Net.Mail` + `Program.cs` `MapMcp("/mcp").RequireAuthorization("mail.send")` (`src/others/Mail.Mcp/`)
- [X] T011 [P] Excel.Mcp `generate_spreadsheet(sheetName, columns, rows) → .xlsx(base64)` ClosedXML + `Program.cs` `MapMcp("/mcp")` (`src/others/Excel.Mcp/`)
- [X] T012 Identity: yeni scope'lar `mail.send` (+ `document.generate`) + client'lar `merchant-api` (`mail.send`) ve `merchant-agent` (`merchant.write`) seed (`src/others/Identity.Server/Config.cs` + `appsettings.json`)
- [X] T013 Merchant aggregate: `MerchantStatus`'a `Provisioning(4)` ekle + alanlar `ReturnUrl`, `ExternalRef`, `CommissionGridReady`, `HasSettlementAccount`, `ActivatedAtUtc` (`src/services/Merchant.Api/Domains/Merchants/Merchant.cs`)
- [X] T014 Merchant aggregate davranışları: `Provision()`, `SetReturnUrl(https)`, `MarkSettlementAccountPresent()`, `MarkCommissionGridReady()`, `TryActivate()` (idempotent 3-koşul) — aynı dosya, depends T013
- [X] T015 [P] Merchant.Api Marten kaydı: `RegisterRequest`, `DomainControlChallenge`, `ActivationTicket`, `OnboardingNotification` document'ları + `merchant.commission` tüketici wiring (`src/services/Merchant.Api/Program.cs`)
- [X] T016 Identity `MerchantClientEventHandler`: `MerchantProvisioned` tüket → OpenIddict client provision (Provisioning demeti = merchant.read/write, charge YOK); `MerchantStatusChanged(Active)` → tam demet (charge gelecekte) (`src/others/Identity.Server/EventHandlers/MerchantClientEventHandler.cs`)

**Checkpoint**: Ortak altyapı hazır — story'ler başlayabilir.

---

## Phase 3: User Story 1 - Aday agent ile başvurur ve sahipliği kanıtlar (Priority: P1) 🎯 MVP

**Goal**: A2A başvuru → descriptor + HTTP-01 challenge → RegisterRequest(Pending) + admin maili. Merchant OLUŞMAZ.

**Independent Test**: Simüle site ile başvuru; challenge yayınlanınca Pending talep + admin maili (Mailpit); challenge'sız denemede talep yok; talep aşamasında merchant yok.

### Tests for User Story 1 (saf domain)

- [X] T017 [P] [US1] Unit: `RegisterRequest` invariant'ları (Create yalnız challenge Passed; Approve/Reject yalnız Pending; dup) (`tests/Merchant.Api.Tests`)
- [X] T018 [P] [US1] Unit: `DomainControlChallenge` Verify (değer eşleşme + TTL + tek-kullanım) (`tests/Merchant.Api.Tests`)

### Implementation for User Story 1

- [X] T019 [P] [US1] `RegisterRequest` aggregate (Create/Approve/Reject, `RegisterRequestStatus`, descriptor kopyası) (`src/services/Merchant.Api/Domains/RegisterRequests/RegisterRequest.cs`)
- [X] T020 [P] [US1] `DomainControlChallenge` aggregate (Issue/Verify, tek-kullanım/TTL, `ChallengeOutcome`) (`.../Domains/RegisterRequests/DomainControlChallenge.cs`)
- [X] T021 [US1] Descriptor çekme + doğrulama (HTTP GET `/.well-known/merchant-descriptor.json`, zorunlu alanlar; erişilemez/eksik → Result hata) (`.../Domains/RegisterRequests/Features/Agent/`)
- [X] T022 [US1] `submit_registration` slice: descriptor doğrula → challenge Issue/Verify → `RegisterRequest.Create` → mükerrer koruma (FR-020) → admin mail tetik (`.../Domains/RegisterRequests/Features/Agent/SubmitRegistration.cs`) — depends T019, T020, T021
- [X] T023 [US1] `registration_status` query slice (domain → Pending/Approved/Rejected) (`.../Features/Agent/RegistrationStatus.cs`)
- [X] T024 [US1] Merchant.Api `/mcp` yüzeyi: `MerchantOnboardingMcpTools` (`submit_registration`, `registration_status`) + `MapMcp("/mcp").RequireAuthorization("merchant.write")` (`src/services/Merchant.Api/McpTools/MerchantOnboardingMcpTools.cs`)
- [X] T025 [US1] Admin "yeni başvuru" bildirim maili: `IMailSender.SendAsync` + `OnboardingNotification` kaydı (FR-005/019) (`.../Domains/RegisterRequests/Features/Agent/`)
- [X] T026 [US1] Merchant.Agent: `register` + `registration_status` skill'leri + agent card + router instructions + Merchant.Api `/mcp` keşfi (`src/agents/Merchant.Agent/`)
- [X] T027 [US1] Aday site = GERÇEK ECommerce E1 (sim yerine): descriptor + challenge + otomatik kayıt (ECommerceWithAgentFramework/src/ui/WebApp/GatewayOnboarding). Eski plan: Simüle aday site: `/.well-known/merchant-descriptor.json` + `/.well-known/merchant-challenge/{token}` sunan minimal host (AppHost resource)

**Checkpoint**: US1 bağımsız çalışır — başvuru + kanıt + Pending talep + admin maili.

---

## Phase 4: User Story 2 - Admin talebi değerlendirir; onayla merchant doğar (Priority: P1)

**Goal**: Admin sayfasından onay → merchant (Provisioning, key üretilir) + ActivationTicket + aktivasyon maili; ret → Rejected.

**Independent Test**: Pending talep açılır; onay yolunda merchant + aktivasyon maili; ret yolunda talep kapanır, merchant yok.

### Tests for User Story 2 (saf domain)

- [X] T028 [P] [US2] Unit: `ActivationTicket` Redeem (tek-kullanım + TTL; ikinci redeem RET) (`tests/Merchant.Api.Tests`)

### Implementation for User Story 2

- [X] T029 [P] [US2] `ActivationTicket` aggregate (Issue/Redeem, tek-kullanım/TTL) (`src/services/Merchant.Api/Domains/Merchants/ActivationTicket.cs`)
- [X] T030 [US2] `approve` slice `[Transactional]`: `RegisterRequest.Approve` → CreateMerchant (statü Provisioning, MerchantKey üret) → `ActivationTicket.Issue` → aktivasyon maili (`IMailSender`) + `OnboardingNotification` (`.../Domains/RegisterRequests/Features/Commands/ApproveRegisterRequest.cs`) — depends T029
- [X] T031 [P] [US2] `reject` slice: `RegisterRequest.Reject(note?)` (`.../Features/Commands/RejectRegisterRequest.cs`)
- [X] T032 [US2] REST uçları: `GET register-requests?status=Pending`, `GET {id}`, `POST {id}/approve`, `POST {id}/reject` — `merchant.read/write` + `AdminPlaneOnly` (`.../Domains/RegisterRequests/RegisterRequestEndpointExtension.cs`)
- [X] T033 [US2] Admin UI "Merchant Talepleri" sayfası (listele + onayla/reddet) + typed `IMerchantOnboardingApiClient` (Aspire discovery `http://merchant-api`) (`src/ui/Admin/Pages/RegisterRequests/` + `src/ui/Admin/Clients/`)

**Checkpoint**: US1+US2 bağımsız — başvuru → onay → merchant doğar + aktivasyon maili.

---

## Phase 5: User Story 3 - Merchant aktivasyon sayfasından MerchantKey'ini alır (Priority: P1)

**Goal**: Aktivasyon linki → key bir kez gösterilir → Provisioning + `MerchantProvisioned` → OpenIddict client → sınırlı token.

**Independent Test**: Aktivasyon linki açılır → key bir kez; ikinci deneme RET; alınan token charge taşımaz; aktivasyon öncesi token RET.

### Tests for User Story 3 (saf domain)

- [X] T034 [P] [US3] Unit: `Merchant.Provision()` geçişi (Provisioning + ActivatedAt) + aktivasyon öncesi statü (`tests/Merchant.Api.Tests`)

### Implementation for User Story 3

- [X] T035 [US3] Merchant.Api redeem ucu `POST merchants/activation/redeem`: bilet doğrula (tek-kullanım/TTL) → `Merchant.Provision()` → `MerchantProvisioned` publish (outbox) → key'i yanıtta **bir kez** dön (`.../Domains/Merchants/Features/Commands/RedeemActivation.cs`)
- [X] T036 [US3] Identity.Server aktivasyon Razor Pages `GET/POST /activation` → redeem çağır → MerchantKey'i **bir kez** göster ("bir daha gösterilmez") (`src/others/Identity.Server/Pages/Activation/`)
- [X] T037 [US3] `MerchantProvisioned` → Identity client provision (Provisioning demeti) doğrulaması + aktivasyon öncesi token verilmezliği (client yokluğu fail-closed) — T016 üzerine canlı doğrulama

**Checkpoint**: US1→US3 (P1 hattı) = MVP. Başvuru → onay → key teslimi → sınırlı token.

---

## Phase 6: User Story 4 - Komisyon gateway-otoriter; merchant Excel ile bilgilendirilir (Priority: P2)

**Goal**: Admin grid Draft → finalize → Ready → `MerchantCommissionGridReady` (koşul #2); MCP yüzeyleri ile agentik Excel maili (orkestratör client 013 dışı).

**Independent Test**: Draft'ta grid okuma "hazır değil" (event/Excel yok); finalize → Ready → event Merchant'a ulaşır; MCP tool'ları tek tek → Ready grid tüm taksit satırlarıyla Excel → Mailpit.

### Tests for User Story 4 (saf domain)

- [X] T038 [P] [US4] Unit: grid finalize bütünlüğü (IsMissing yok + BelowBankCeiling yok → Ready; eksikse RET) + Draft/Ready geçişi (`tests/Commission.Api.Tests`)

### Implementation for User Story 4

- [X] T039 [US4] Commission grid Draft/Ready statüsü + `FinalizeGrid` command (bütünlük doğrula → Ready) + `MerchantCommissionGridReady` publish `[Transactional]` (outbox) (`src/services/Commission.Api/Domains/MerchantCommissions/Features/Commands/FinalizeMerchantCommissionGrid.cs`)
- [X] T040 [US4] Commission.Api `/mcp`: `get_merchant_commission_grid` (Draft/Ready statü + tüm banka-destekli taksit satırları) + `MapMcp("/mcp").RequireAuthorization("commission.read")` (`src/services/Commission.Api/McpTools/MerchantCommissionMcpTools.cs`)
- [X] T041 [P] [US4] Merchant.Api `/mcp`: `get_merchant` read tool ekle (`src/services/Merchant.Api/McpTools/MerchantOnboardingMcpTools.cs`)
- [X] T042 [US4] Merchant.Api `MerchantCommissionGridReadyHandler` (tekil `...Handler`) → `MarkCommissionGridReady` + `TryActivate` (idempotent, durable inbox) (`src/services/Merchant.Api/ReadModels/MerchantCommissionGridReadyHandler.cs`)
- [X] T043 [US4] Admin UI: komisyon grid'ine **Finalize** aksiyonu (Draft→Ready) (`src/ui/Admin/Pages/MerchantCommissions/`)

**Checkpoint**: Grid finalize → koşul #2 event; MCP yüzeyleri Excel orkestrasyonu için hazır.

---

## Phase 7: User Story 5 - Koşullar tamamlanınca otomatik Active (Priority: P2)

**Goal**: settlement + grid-Ready + ReturnUrl (3/3) → otomatik Active → `MerchantStatusChanged(Active)` → tam demet.

**Independent Test**: 3 koşul sırayla; üçüncüde ≤1dk otomatik Active + yeni token tam yetki; 2 koşulla Active olmaz; HTTP ReturnUrl RET.

### Tests for User Story 5 (saf domain)

- [X] T044 [P] [US5] Unit: `TryActivate()` 3-koşul (2/3 → Active değil; 3/3 → Active; tekrar → no-op) + `SetReturnUrl` HTTPS doğrulama (`tests/Merchant.Api.Tests`)

### Implementation for User Story 5

- [X] T045 [US5] `PUT merchants/{merchantId}/return-url` (HTTPS doğrula → `SetReturnUrl` → `TryActivate`) — `merchant.write` + `MerchantScoped` (`.../Domains/Merchants/Features/Commands/SetReturnUrl.cs`)
- [X] T046 [US5] CreateSettlementAccount handler'ına kanca: ilk hesap → `MarkSettlementAccountPresent` + `TryActivate` (aynı `[Transactional]`, BC-içi) (`src/services/Merchant.Api/Domains/SettlementAccounts/Features/Commands/CreateSettlementAccount.cs`)
- [X] T047 [US5] `TryActivate` Active'e geçince `MerchantStatusChanged(Active)` publish (outbox) → Identity tam demet (T016 hattı) (`.../Domains/Merchants/`)

**Checkpoint**: US5 — 3 koşul → otomatik Active + tam yetki.

---

## Phase 8: User Story 6 - Merchant externalRef ile eşleyebilir (Priority: P3)

**Goal**: Opak `externalRef` kabul/sakla/aynen dön.

**Independent Test**: externalRef'li istek → sorguda aynen döner; externalRef'siz → normal çalışır.

### Tests for User Story 6 (saf domain)

- [X] T048 [P] [US6] Unit: `externalRef` round-trip (set → aynen dön; null opsiyonel) (`tests/Merchant.Api.Tests`)

### Implementation for User Story 6

- [X] T049 [US6] Merchant'a dönük kayıt uçlarında opsiyonel `externalRef` alanı: kabul + sakla + yanıtta aynen dön (`src/services/Merchant.Api/Domains/Merchants/Features/`)

**Checkpoint**: Tüm story'ler bağımsız çalışır.

---

## Phase 9: Polish & Cross-Cutting

- [X] T050 [P] Wolverine tekil-`...Handler` canlı doğrulama: yeni tüketicilerde log "Successfully processed", "No known handler" YOK (T016, T042)
- [X] T051 [P] Dual-write/outbox doğrulama (research D13): grid finalize + event aynı tx; Active geçişi + event aynı tx; tüketici idempotent
- [X] T052 [P] FR-019 mail başarısızlık görünürlüğü: `OnboardingNotification` Failed kayıtları + admin görünürlüğü/retry
- [X] T053 [P] README/CLAUDE.md kimlik/onboarding bölümü güncelle (Provisioning, Mail.Mcp/Excel.Mcp, RegisterRequest)
- [X] T054 Quickstart S1–S6 canlı doğrulama (`specs/013-merchant-onboarding/quickstart.md`) + agentik Excel tool-zinciri (client'sız, tool-bazında)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: bağımsız, hemen başlar.
- **Foundational (P2)**: Setup sonrası — TÜM story'leri bloklar.
- **User Stories (P3+)**: Foundational sonrası. P1 hattı sıralı bağımlı (US1→US2→US3 aynı akışı zincirler); P2/P3 Foundational sonrası paralelleşebilir.
- **Polish**: istenen story'ler bitince.

### User Story Dependencies

- **US1 (P1)**: Foundational sonrası. Bağımsız (başvuru kapısı).
- **US2 (P1)**: US1'in ürettiği RegisterRequest'i tüketir (onay). Akış-zinciri: US1 → US2.
- **US3 (P1)**: US2'nin ürettiği merchant + ActivationTicket'i tüketir. US2 → US3.
- **US4 (P2)**: Foundational sonrası bağımsız (grid + MCP). US5 koşul #2'sini besler.
- **US5 (P2)**: settlement (mevcut) + US4 grid-ready event + ReturnUrl birleşir. TryActivate (Foundational T014) üstüne.
- **US6 (P3)**: Foundational sonrası bağımsız.

### Within Each Story

- Testler (saf domain) implementasyondan önce yazılıp FAIL etmeli.
- Aggregate/VO → slice/handler → endpoint/MCP tool → agent/UI.

### Parallel Opportunities

- Setup: T002, T003, T005 [P].
- Foundational: T006, T007, T011 [P]; T008→T009 sıralı; T013→T014 sıralı.
- US1 aggregate'leri T019, T020 [P]; testler T017, T018 [P].
- Foundational bitince US4 ve US6 diğer story'lerden bağımsız paralel yürüyebilir.

---

## Parallel Example: User Story 1

```bash
# Testler (saf domain) birlikte:
Task: "Unit: RegisterRequest invariant'ları (tests/Merchant.Api.Tests)"
Task: "Unit: DomainControlChallenge Verify/TTL (tests/Merchant.Api.Tests)"

# Aggregate'ler birlikte:
Task: "RegisterRequest aggregate (Domains/RegisterRequests/RegisterRequest.cs)"
Task: "DomainControlChallenge aggregate (Domains/RegisterRequests/DomainControlChallenge.cs)"
```

---

## Implementation Strategy

### MVP (P1 hattı: US1 + US2 + US3)

1. Phase 1 Setup → Phase 2 Foundational.
2. US1 (başvuru + kanıt) → US2 (onay + merchant doğar) → US3 (key teslimi + sınırlı token).
3. **DUR ve DOĞRULA**: quickstart S1–S3. Bu, onboarding'in çekirdeği — merchant kimlik alır.

### Incremental

1. Setup + Foundational → temel hazır.
2. + US1→US2→US3 (MVP: başvurudan key teslimine) → quickstart S1–S3.
3. + US4 (komisyon grid + Excel yüzeyleri) → S4.
4. + US5 (otomatik Active) → S5.
5. + US6 (externalRef) → S6.

### Notlar

- [P] = farklı dosya, bağımlılık yok.
- Foundational T013/T014 (Merchant statü + TryActivate) US3/US5 için kritik — erken bitir.
- Wolverine tüketici: `public static class ...Handler` (TEKİL), `public static async Task Handle(...)`.
- Cross-BC hepsi outbox (`[Transactional]` + event aynı commit); tüketici idempotent.
- MerchantKey: yalnız redeem yanıtı + aktivasyon sayfası (bir kez); başka kanala çıkmaz.

## Canlı doğrulama durumu (2026-08-08, AppHost smoke)

- Build 0 hata; domain testleri 75 (Merchant) + 44 (Commission) yeşil.
- AppHost ayağa kalktı: Postgres/RabbitMQ/Identity/Merchant.Api(5202)/Commission.Api(5203)/
  Mail.Mcp/Excel.Mcp/Mailpit(8025) — startup log'da exception/"No known handler" YOK.
- Identity token (admin-ui) verildi; Merchant.Api JWKS doğrulama + AdminPlaneOnly + Marten yeni
  aggregate şeması → `GET register-requests` 200 (boş); token'sız 401. Commission `finalize` boş
  grid → 400 (bütünlük RET, event yok). → T037/T050/T051/T052 wiring+kod düzeyinde doğrulandı.
- **T054 (kaldı, elle)**: tam S1–S6 uçtan-uca — LLM agent (OpenAI:ApiKey) + Identity aktivasyon
  tarayıcı sayfası + Mailpit görsel kontrol gerektirir. ECommerce E1 otomatik sürüşüyle (POST
  /gateway-onboarding/register) deterministik koşulabilir; DropShop McpUrl=http://localhost:5202/mcp.

## S1–S6 CANLI doğrulama (2026-08-08, iki sistem: DropShop + ECommerce)

Tümü GEÇTİ:
- S1: ECommerce POST /gateway-onboarding/register → DropShop /mcp submit_registration (otomatik
  challenge) → Pending + admin maili (Mailpit).
- S2: admin approve → merchant Provisioning + aktivasyon maili.
- S3: Identity /activation tarayıcı sayfası → MerchantKey bir kez + ikinci redeem 400 (tek-kullanım).
- S4: merchant-commissions bulk + finalize → Ready → MerchantCommissionGridReady event.
- S5: settlement + return-url + grid-ready → OTOMATİK Active (ilk poll'de); Active token scope
  JSON dizisi [merchant.read, merchant.write] + merchant_id claim, charge yok (fail-closed).
- Canlı bulunan+düzeltilen 5 bug (hepsi commit): (1) Mail/Excel/Agent 5000 port çakışması;
  (2) IMailSender DI (Scrutor Common taramadı → explicit register); (3) challenge Update→Store
  (NonExistentDocument); (4) Identity aktivasyon merchant-api service-discovery → sabit port;
  (5) descriptor fetch dev-cert kabulü.
