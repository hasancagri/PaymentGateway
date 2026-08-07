# Tasks: OpenIddict Migrasyonu + BC API Yetkilendirmesi

**Input**: Design documents from `/specs/011-openiddict-migration/`

**Prerequisites**: plan.md, spec.md, research.md (D1-D10), data-model.md, contracts/auth-model.md, quickstart.md

**Tests**: Test task'ı YOK — domain mantığı yok (altyapı feature'ı); doğrulama quickstart canlı senaryolarıyla.

**Organization**: User story bazlı; her faz bağımsız teslim edilebilir artış.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (paket zemini)

**Purpose**: CPM'de motor değişimi — sonraki her şey bunun üstüne derlenir.

- [X] T001 Directory.Packages.props: 3 Duende.IdentityServer* paketini sil; OpenIddict.AspNetCore 7.6.0 + OpenIddict.EntityFrameworkCore 7.6.0 ekle
- [X] T002 src/others/Identity.Server/Identity.Server.csproj: Duende PackageReference'larını OpenIddict ile değiştir (EF/Npgsql/Identity kalır)

---

## Phase 2: Foundational (eski içerik temizliği — blocking)

**Purpose**: Duende'ye referans veren kopya içerik silinmeden proje derlenmez; tüm story'ler buna bağlı.

- [X] T003 src/others/Identity.Server/Pages/ tamamını sil (login/consent/device/ciba/grants/serversidesessions/diagnostics — Duende quickstart UI)
- [X] T004 [P] src/others/Identity.Server/ApiKeys/ + Data/ApiKey.cs + Data/UserScope.cs sil (UserKey alt sistemi — FR-008)
- [X] T005 [P] Duende kalıntılarını sil: eski Config.cs içeriği, Data/Migrations/ altındaki Duende migration'ları, keys/ klasörü (varsa)

**Checkpoint**: Identity.Server iskeleti boş; US1 yeniden kurar.

---

## Phase 3: User Story 1 — Kimlik motoru OpenIddict üzerinde token verir (P1) 🎯 MVP

**Goal**: `connect/token` client_credentials ile çalışır; issuer https://localhost:5101; scope claim JSON dizisi.

**Independent Test**: quickstart S1 — token al, payload doğrula (iss/sub/aud/scope-dizi); negatifler 400.

- [X] T006 [US1] src/others/Identity.Server/Data/ApplicationDbContext.cs: ApiKey/UserScope DbSet'lerini çıkar; options.UseOpenIddict() context'e bağlanır (Program'da)
- [X] T007 [US1] src/others/Identity.Server/Config.cs yeniden yaz: 6 scope + ScopeResources haritası + ClientSeed listesi (admin-ui, payment-agent; secret'lar IConfiguration'dan — D4)
- [X] T008 [P] [US1] src/others/Identity.Server/Connect/ScopeClaimArrayHandler.cs: 029'dan birebir taşı (TokenTypeIdentifiers URN guard + OidcClaimDestinations M2M hali — D3)
- [X] T009 [P] [US1] src/others/Identity.Server/Connect/TokenEndpoint.cs: yalnız client_credentials dalı (sub=client_id, SetScopes, ListResourcesAsync→aud, SetDestinations — D1/D9)
- [X] T010 [US1] src/others/Identity.Server/Connect/SeedHostedService.cs: idempotent scope+client seed (RBAC/BootstrapAdmin YOK; secret config'ten; yalnız statik listeyi upsert — D4/D9)
- [X] T011 [US1] src/others/Identity.Server/Program.cs yeniden yaz: EF+Identity store, AddOpenIddict (SetIssuer 5101, yalnız token ucu, dev cert, DisableAccessTokenEncryption, ScopeClaimArrayHandler), migrate+seed; RazorPages/login YOK (D1/D2/D6)
- [X] T012 [P] [US1] src/others/Identity.Server/Properties/launchSettings.json: applicationUrl https://localhost:5101
- [X] T013 [US1] EF tek Initial migration üret (Identity çekirdeği + OpenIddict tabloları; dotnet ef migrations add Initial)
- [X] T014 [US1] src/aspire/AppHost/AppHost.cs: identityDb database + identity-server resource (WithReference(identityDb)+WaitFor; https launch profili)
- [X] T015 [US1] Canlı doğrulama: quickstart S1 (token + payload + invalid_client/invalid_scope negatifleri)

**Checkpoint**: IdP tek başına çalışır — MVP teslim edilebilir.

---

## Phase 4: User Story 2 — BC API'leri açık yetkiyle korunur (P2)

**Goal**: 3 API JWT bearer doğrular; her uç scope beyan eder; Admin BFF makine token'ıyla kesintisiz.

**Independent Test**: quickstart S2 (401/403/200) + S3 (Admin ekran turu) + S6 (çoklu-scope regresyonu).

- [X] T016 [US2] src/others/Common/Utils/Constants/AuthorizationScopes.cs yeniden yaz: 6 gateway scope sabiti (ECommerce seti silinir — FR-003)
- [X] T017 [P] [US2] src/services/Merchant.Api: Program.cs'e AddAuthenticationAndAuthorizationExtension(config, merchant scope'ları) + UseAuthentication/UseAuthorization + appsettings.json IdentityOption (Address=https://localhost:5101, Audience=merchant.api)
- [X] T018 [P] [US2] src/services/Commission.Api: aynı kurulum (Audience=commission.api)
- [X] T019 [P] [US2] src/services/Payment.Api: aynı kurulum (Audience=payment.api)
- [X] T020 [P] [US2] Merchant.Api endpoint'leri: MerchantEndpointExtension + SettlementAccountEndpointExtension gruplarına RequireAuthorization (GET→merchant.read, mutasyon→merchant.write — contracts matrisi)
- [X] T021 [P] [US2] Commission.Api endpoint'leri: Bank/BankCommission/MerchantCommission extension'larına RequireAuthorization (commission.read/write)
- [X] T022 [P] [US2] Payment.Api REST endpoint'leri: PosAccount + BinCard extension'larına RequireAuthorization (payment.read; import→payment.write)
- [X] T023 [US2] src/aspire/AppHost/AppHost.cs: 3 API'ye WithReference(identityServer) + WaitFor(identityServer)
- [X] T024 [US2] src/ui/Admin: AdminTokenHandler.cs (client_credentials, static cache, -30 sn yenileme — SagaTokenHandler deseni D7); Program.cs'te 4 typed client'a AddHttpMessageHandler; appsettings IdentityOption:Address + AdminAuth:ClientId/ClientSecret
- [X] T025 [US2] Canlı doğrulama: quickstart S2 (token'sız 401, yanlış scope 403, doğru scope 200 — üç API'de)
- [X] T026 [US2] Canlı doğrulama: quickstart S3 (Admin ekran turu) + S6 (çoklu-scope token üç API'de 200)

**Checkpoint**: REST yüzeyi korumalı; Admin kesintisiz. (MCP henüz açık — US3'te kapanır, akış yeşil kalır.)

---

## Phase 5: User Story 3 — Agent akışı yetkili olarak sürer (P3)

**Goal**: /mcp yüzeyi payment.write ister; Payment.Agent token edinir; A2A akışı uçtan uca.

**Independent Test**: quickstart S4 — A2A taksit akışı + "sessiz başarı yok" (secret boz → anlaşılır hata).

- [X] T027 [US3] src/services/Payment.Api/Program.cs: MapMcp("/mcp").RequireAuthorization(payment.write) (contracts: tek policy)
- [X] T028 [US3] src/agents/Payment.Agent: AgentTokenHandler (D7 deseni) + McpToolProvider'da MCP transport'a token'lı HttpClient; appsettings IdentityOption:Address + AgentAuth:ClientId/ClientSecret
- [X] T029 [US3] src/aspire/AppHost/AppHost.cs: payment-agent'a WithReference(identityServer) + WaitFor(identityServer)
- [X] T030 [US3] Canlı doğrulama: quickstart S4 (A2A akışı Bearer'lı; secret bozuk → akış anlaşılır hata, sessiz başarı yok)

**Checkpoint**: Tüm istemciler yetkili; sistemde korumasız iş ucu kalmadı.

---

## Phase 6: Polish & Cross-Cutting

- [X] T031 [P] src/others/Common: Auths/ApiKeyAuthenticationHandler.cs + ApiKeyAuthenticationMiddleware.cs + ApiKeyAuthenticationOptions.cs + Extensions/ApiKeyAuthenticationExtension.cs sil (ölü kopya — FR-008; gateway'in AddAuthenticationAndAuthorizationExtension çağrısı KALIR)
- [X] T032 [P] .specify/memory/constitution.md: İlke V amendment — "Duende IdentityServer"→"OpenIddict tabanlı merkezi Identity servisi"; TODO(AUTHZ_MODEL) daralt (makine düzlemi=scope-tabanlı KARARLI; insan/rol + merchant düzlemi açık); MINOR bump + Sync Impact Report
- [X] T033 [P] CLAUDE.md güncelle: "Bilinçli ertelemeler → Yetkilendirme yok" kaldır; Identity.Server + auth modeli + 5101 notu ekle
- [X] T034 Kapanış doğrulama: quickstart S5 (grep duende boş, ApiKey kalıntısı yok, /Account/Login 404) + dotnet build + dotnet test (Merchant/Commission testleri yeşil)

---

## Dependencies

- Phase 1 → Phase 2 → Phase 3 (US1) → Phase 4 (US2) → Phase 5 (US3) → Phase 6.
- US2, US1'in canlı IdP'sine bağlı (token almadan 401/403 doğrulanamaz). US3, US2'nin Payment.Api bearer
  kurulumuna (T019) bağlı. Story içi: T006-T012 → T013 (migration derlenen koda bakar) → T014 → T015.
- T016 (AuthorizationScopes) T017-T022'den önce (sabitleri onlar kullanır).

## Parallel Execution Examples

- Phase 2: T004 + T005 paralel (farklı klasörler).
- US1: T008 + T009 + T012 paralel (T007 sonrası).
- US2: T017 + T018 + T019 paralel; ardından T020 + T021 + T022 paralel.
- Polish: T031 + T032 + T033 paralel.

## Implementation Strategy

- **MVP = US1** (Phase 1-3): IdP tek başına token verir — bağımsız kanıtlanır (S1).
- Sonra US2 (koruma + Admin), sonra US3 (agent + MCP). Her checkpoint'te sistem çalışır durumda:
  US2 sonunda MCP bilinçli açık (akış kırılmaz), US3 kapatır.
- Canlı doğrulama task'ları (T015/T025/T026/T030/T034) atlanmaz — proje konvansiyonu (entegrasyon
  testi yok, quickstart kanıtı var).