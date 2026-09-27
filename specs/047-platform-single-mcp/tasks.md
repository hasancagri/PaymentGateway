# Tasks: Tek MCP Yüzeyinde PG — Store Fasadına Bağlanma + Platform Kimliği

**Input**: Design documents from `/specs/047-platform-single-mcp/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R8), data-model.md, contracts/mcp-surface.md, quickstart.md

**Tests**: İLKE VI — saf mantık (budama kuralı `IsToolVisible`) test-first; kimlik/endpoint/fasat wiring canlı doğrulama (quickstart S1-S5).

**Organization**: US1 mekanizmanın ana gövdesi (platform kimliği + budama + fasat bağlama); US2 doğrulama ağırlıklı; US3 söküm. Üç repo: AgentPlatform PR'ı ÖNCE merge (R8).

## Phase 1: Setup

- [ ] T001 Üç repoda dal aç: PG `047-platform-single-mcp` (mevcut), EC `090-pg-facade-downstream` (sıradaki numara EC'de teyit et), AgentPlatform `002-pg-app-registry`; üçünde baseline `dotnet build` + `dotnet test` yeşil kaydet.

## Phase 2: Foundational (blocking)

**AgentPlatform (AYRI PR — ÖNCE merge; R1/R8):**

- [X] T002 `../AgentPlatform/src/Identity.Server/appsettings.json` + `appsettings.Development.json` — `AppRegistry.Apps` += `pg` app'i: 5 scope (data-model.md tablosu: merchant.read/write/admin→merchant.api, commission.read/write→commission.api), `admin` rolüne additive katkı, Clients boş; `external-admin-agent` + `mcp-gateway-discovery` Scopes listelerine PG demeti eklenir. Validator (`ValidateOnStart`) yeşil.
- [ ] T003 AgentPlatform canlı smoke: admin istemcisiyle login → token'da PG scope'ları + aud merchant.api/commission.api VAR; DCR şablon istemcisi PG scope'u ALAMAZ (tavan); `dotnet build` + test yeşil → AgentPlatform PR aç, merge et.

**PG ortak taban:**

- [X] T004 [P] TEST-FIRST: `tests/Merchant.Api.Tests/ScopePruningTests.cs` — saf budama kuralı: haritada olmayan tool görünür; haritadaki tool yalnız scope claim'i varsa görünür; token'sız/claim'siz kullanıcı admin tool göremez; kısmi scope (yalnız commission.read) yalnız kendi tool'unu görür. Kırmızı koş.
- [X] T005 `src/others/Common/Extensions/McpScopePruningExtension.cs` (YENİ) — EC 085 `IsToolVisible` bilinçli kopyası (R4); T004 yeşil.
- [X] T006 `src/others/Common/Options/PlatformIdentityOption.cs` (YENİ, Address+Audience POCO) + `src/others/Common/Extensions/AuthenticationExtension.cs` — `"Platform"` adlı ikinci JwtBearer şeması + `Platform:<scope>` policy'leri (`AddAuthenticationSchemes("Platform")` + scope claim; R3). REST policy'leri default şemada değişmez.
- [X] T007 [P] Tool→scope holder'ları: `src/services/Merchant.Api/Mcp/MerchantAdminSurface.cs` (10 tool → merchant.admin) + `src/services/Commission.Api/Mcp/CommissionAdminSurface.cs` (1 okuma → commission.read, 3 yazma → commission.write) — kaynak `[RequiredScope]` attribute'larıyla birebir (data-model.md).

## Phase 3: US1 — Admin tek kayıtla PG tool'larını störde görür (P1)

**Goal**: PG tool'ları fasat listesinde; platform token'ıyla çalışır. **Independent test**: quickstart S1.

Senaryo: Admin Ali, Claude Desktop'taki tek `store` kaydıyla login olur; "bekleyen merchant başvurularını göster" der (`admin_get_pending_registrations`), birini onaylar (`admin_approve_registration`) — dün üç kayıtla yaptığını tek oturumda yapar.

- [X] T008 [P] [US1] `src/services/Merchant.Api/Program.cs` + `appsettings*.json` — PlatformIdentityOption bağla; `/mcp` → `Platform:merchant.write` policy'sine geç (PG IdP token'ı 401); `WithHttpTransport(ConfigureSessionOptions=...)` ile `MerchantAdminSurface.ToolScopeMap` budaması (Catalog.Api 085 emsali).
- [X] T009 [P] [US1] `src/services/Commission.Api/Program.cs` + `appsettings*.json` — aynı desen; `/mcp` → `Platform:commission.read`; `CommissionAdminSurface.ToolScopeMap` budaması.
- [X] T010 [US1] EC repo: `src/others/Common/Utils/Constants/AuthorizationScopes.cs` — PG scope sabitleri (MerchantRead/Write/Admin, CommissionRead/Write; PG bloğu yorumla) + `src/agents/Mcp.Gateway/FacadeScopes.cs` `All` += 5 PG scope (079 tuzak yorumu korunur).
- [X] T011 [US1] EC repo: `src/agents/Mcp.Gateway/appsettings.Development.json` (+prod muadili) — `Downstreams` += `pg-merchant http://localhost:5202/mcp`, `pg-commission http://localhost:5203/mcp` (SABİT URL, R5); `DiscoveryScope` += PG demeti.
- [X] T012 [US1] Build guard: PG + EC tam `dotnet build` && `dotnet test` yeşil (T004 dahil).
- [ ] T013 [US1] Canlı S1 (quickstart): üç sistem ayakta; Desktop'ta pg-* kayıtları silinmiş, tek store kaydı + `~/.mcp-auth` temiz; admin login → birleşik listede EC + PG 14 tool; `admin_get_merchants` + `admin_update_merchant` canlı PASS.

## Phase 4: US2 — Yetkisiz oturum PG tool'u ne görür ne çağırır (P1)

**Goal**: Sızıntı 0; yüzey başına tek otorite. **Independent test**: quickstart S2-S4.

Senaryo: Müşteri Ayşe aynı store kaydıyla login olur; tool listesinde PG'den TEK satır yoktur; kurcalayıp `admin_get_merchants`'ı adıyla çağırırsa PG 403 döner — merchant verisi müşteri oturumuna sızmaz.

- [ ] T014 [US2] Canlı S2: müşteri login → PG tool 0; ham `tools/call admin_get_merchants` → yetki hatası (Merchant.Api log'unda RequiredScope reddi izi).
- [ ] T015 [US2] Canlı S3 (kısmi yetki): platform rol ekranında yalnız `commission.read`'li test rolü → listede PG'den YALNIZ `admin_get_commission_policy`; scope başına budama kanıtı.
- [ ] T016 [US2] Canlı S4 (yüzey başına tek otorite, FR-005): PG `admin-ui` m2m token'ı → `http://localhost:5202/mcp` 401; platform token'ı → Merchant.Api REST admin ucu 401. İki yön de red.

## Phase 5: US3 — Söküm + regresyonsuz mevcut akışlar (P2)

**Goal**: Fazla MCP uçları + ölü kimlik yüzeyi ölür; para yolu/S2S bugünle birebir. **Independent test**: quickstart S5.

Senaryo: Sistemci Hasan eski kapıları yoklar: PG IdP'den `external-admin-agent` token isteği invalid_client, Payment `/mcp` 404; ama mağazadan gerçek bir sipariş ödemesi (iyzico sandbox) kesintisiz Confirmed olur.

- [X] T017 [P] [US3] `src/services/Payment.Api/Program.cs` — `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` bloğu + `MapMcp("/mcp")` satırı SİL (payment.write REST policy'leri kalır); Payment.Api'de `McpServerToolType` kalıntısı olmadığını grep'le teyit.
- [X] T018 [P] [US3] `src/services/gateway/Gateway/appsettings.Development.json` + `appsettings.json` — tüm `*-mcp-route` girişleri SİL (EC kalıntısı; kalan bayat REST rotalarının genel temizliği AYRI iş — dokunma).
- [X] T019 [US3] `src/others/Identity.Server/Config.cs` — `external-admin-agent` seed'i çıkar, `RetiredClientIds` += `"external-admin-agent"` (044 prune); `Connect/AdminAgentApplicationManager.cs` + `ClaudeCallbackRedirectUris` sabiti SİL (tek tüketicisi oydu); `scripts/claude-desktop-pg-mcp.sh` SİL; repo içi referans taraması sıfır (specs/ hariç).
- [ ] T020 [US3] Canlı S5: `external-admin-agent` token isteği invalid_client; `:5201/mcp` + PG gateway `/mcp/*` 404; hosted ödeme uçtan uca (start_payment → iyzico sandbox → callback → Confirmed — 085 T030'da PAS geçilen doğrulama BURADA kapanır); onboarding S2S + Admin hassas-veri sayfası PG IdP token'larıyla PASS.

## Phase 6: Polish & Cross-Cutting

- [X] T021 [P] `.specify/memory/constitution.md` — İlke V'e MINOR amendment: insan/agent düzlemi platform IdP'sinden (AgentPlatform), makine/merchant düzlemi PG Identity.Server'da; issuer cümlesi düzlem ayrımıyla güncellenir; Sync Impact Report işlenir (plan Complexity kaydı).
- [X] T022 [P] PG `CLAUDE.md` — BC haritası (Merchant/Commission satırlarına fasat-downstream + platform-token notu; Payment satırından `/mcp` bahsi düşer), altyapı bloğu (dış MCP istemcisi → store fasadı üzerinden), 044 mcp-remote köprü bahsini kaldır, origin spec güncelle.
- [X] T023 [P] EC repo `CLAUDE.md` — Mcp.Gateway satırına pg-merchant/pg-commission downstream'leri + PG scope ilanı notu (085 bölümüne ek).
- [X] T024 Regresyon turu: üç repoda `dotnet build` + `dotnet test` yeşil; PG `scripts/check-claude-spec-links.sh` + `scripts/check-flow-links.sh` yeşil; memory güncelle (047 durumu + 048 adayı: tam IdP konsolidasyonu + Merchant.Identity rename ayrı PR).

## Dependencies

- T001 → T002-T007 (Foundational) → US1 (T008-T013) → US2 (T014-T016) → US3 (T017-T020) → Polish.
- AgentPlatform zinciri T002→T003; T003 merge OLMADAN T013 canlıya çıkamaz (fasat PRM ilanı platforma bağımlı) — PG/EC kodu paralel yazılabilir.
- T004→T005; T006/T007 → T008/T009; T010→T011→T013. US2 fiilen US1 koduyla gelir (doğrulama). US3 US1'den bağımsız kod, canlısı T013 sonrası anlamlı.
- Merchant.Identity rename bu feature'a GİRMEZ (ayrı PR, spec kararı).

## Parallel Execution Examples

- T004 ‖ T007 (test-first + holder'lar, farklı dosyalar).
- T008 ‖ T009 (iki BC, 1-servis-1-agent deseni).
- T017 ‖ T018 (söküm, farklı dosyalar); T021 ‖ T022 ‖ T023 (üç doküman).

## Implementation Strategy

MVP = Foundational + US1 (T001-T013): tek kayıt + tek login + birleşik liste canlı — tek başına teslim edilebilir. US2 ağırlıkla doğrulama (mekanizma US1'de). US3 söküm US1 canlısından sonra güvenle yapılır (geri dönüş kapısı açık kalır). Riskin en yükseği IdP davranışı değil (platform SALT CONFIG); en olası sürpriz platform token claim biçimi → S1'de erken yakalanır (R8 risk notu).
