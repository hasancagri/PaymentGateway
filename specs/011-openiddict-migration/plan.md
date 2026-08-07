# Implementation Plan: OpenIddict Migrasyonu + BC API Yetkilendirmesi

**Branch**: `011-openiddict-migration` | **Date**: 2026-08-07 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/011-openiddict-migration/spec.md`

## Summary

Uyur durumdaki (orkestre edilmeyen, ECommerce'ten kopyalanmış) Duende Identity.Server, OpenIddict 7.6
üzerine minimal bir **yalnız-makine (client_credentials) IdP** olarak yeniden kurulur; ASP.NET Identity
kullanıcı deposu kalır. Üç BC API'si (Payment, Merchant, Commission) ilk kez JWT bearer + scope policy ile
korunur; Admin BFF ve Payment.Agent makine token'ı edinir. ECommerce kalıntıları (ApiKeys/UserKey, eski
scope seti, kullanılmayan login/consent sayfaları) silinir. Blueprint: ECommerce 029 (davranış-birebir
referans) — ama insan login'i olmadığı için yüzey 029'dan çok daha küçük: tek uç `connect/token`.

**G2 hazırlığı (merchant = MerchantKey ile istemci)**: merchant'lar G2'de client_credentials istemcisi olur
(client_id=merchantId, client_secret=MerchantKey, status-gated scope). 011 bunu KURMAZ ama zeminini döşer:
client store DB-tabanlıdır (OpenIddict application tablosu — dinamik provizyona açık), seed idempotent ve
statik listeyle sınırlıdır (G2'nin dinamik kayıtlarını ezmez), token ucunun client_credentials dalı
`sub=client_id` verir (merchant_id claim'i G2'de eklenir), scope registry genişletilebilir (`cards.write`,
`charge` G2'de eklenir).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: OpenIddict.AspNetCore 7.6.0 + OpenIddict.EntityFrameworkCore 7.6.0 (CPM'e eklenir);
Duende.IdentityServer* 7.4.3 (3 paket) CPM'den silinir. ASP.NET Identity (EF Core, Npgsql) kalır.

**Storage**: Postgres `identityDb` (yeni Aspire database; EF Core — anayasanın izole-altyapı istisnası).
Tek clean Initial migration (Identity çekirdeği + OpenIddict tabloları; ApiKeys/UserScope/RoleScope YOK).

**Testing**: Saf domain birim testi yok (domain mantığı yok — altyapı feature'ı); doğrulama quickstart
canlı senaryolarıyla (proje konvansiyonu: handler/HTTP entegrasyonu elle doğrulanır).

**Target Platform**: Aspire orkestrasyonu (AppHost); Identity.Server yeni resource olarak eklenir.

**Project Type**: Mikroservis altyapı feature'ı — 1 IdP + 3 API + 2 istemci (Admin BFF, Payment.Agent) + AppHost.

**Performance Goals**: Yok (token verme sıcak yol değil; istemciler token'ı süresine 30 sn kala yenileyip
cache'ler — SagaTokenHandler deseni).

**Constraints**: Issuer/Authority birebir tutarlılığı; HTTPS zorunlu (dev cert). Port **5101** seçilir —
ECommerce Identity 5001 kullanır ve A2A senaryosunda iki sistem AYNI ANDA koşar (postgres 5433 emsali).
Access token düz imzalı JWT (`DisableAccessTokenEncryption`) + scope claim JSON dizisi (029 R3 tuzağı).

**Scale/Scope**: 6 scope, 2 iç istemci, 3 korunan API (~7 endpoint grubu + 1 MCP yüzeyi), 1 anayasa amendment.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Değerlendirme | Sonuç |
|------|---------------|-------|
| I. BC izolasyonu | Identity ayrı servis + kendi `identityDb`'si; hiçbir BC başka DB'ye erişmez. Token doğrulama JWKS üzerinden (paylaşılan DB yok). | PASS |
| II. Zengin domain | Domain aggregate yok — altyapı feature'ı. Identity EF modeli anayasanın açık istisnası. | PASS (N/A) |
| III. Vertical Slice + CQRS | BC API'lerde yalnız endpoint korumaları eklenir (slice yapısı değişmez). IdP uçları minimal API extension'larıyla map'lenir (029 deseni). | PASS |
| IV. Result pattern | 401/403 framework-düzeyi kimlik/yetki cevabıdır; Result pattern beklenen İŞ hatası içindir. ECommerce paritesi korunur. | PASS |
| V. Merkezi kimlik + açık yetki | Feature bu ilkeyi İLK KEZ uygular. İlke metni "Duende IdentityServer" anar → **amendment gerekir** (implement fazında, gerekçeli). TODO(AUTHZ_MODEL) makine düzlemi için netleşir: scope-tabanlı; insan/rol + merchant düzlemi sonraki feature'lara kalır (TODO açık, daraltılmış notla). | PASS (amendment planlı) |
| VI. Spec-driven | Tam feature → tam artefakt seti (bu plan + research + data-model + contracts + quickstart + tasks). | PASS |

Post-design re-check: ihlal yok; Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/011-openiddict-migration/
├── plan.md              # Bu dosya
├── research.md          # Faz 0: kararlar (blueprint farkları, port, secret yönetimi, G2 hazırlığı)
├── data-model.md        # Faz 1: istemci/scope modeli + identityDb şeması
├── quickstart.md        # Faz 1: canlı doğrulama senaryoları
├── contracts/
│   └── auth-model.md    # Faz 1: token ucu + scope matrisi + korunan yüzeyler
└── tasks.md             # Faz 2 (/speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/others/Identity.Server/            # YENİDEN KURULUR (minimal M2M IdP)
├── Program.cs                         # OpenIddict server (yalnız token ucu) + EF + seed
├── Config.cs                          # scope/resource haritası + ClientSeed listesi (secret config'ten)
├── Connect/
│   ├── TokenEndpoint.cs               # yalnız client_credentials dalı (029'un alt kümesi)
│   ├── ScopeClaimArrayHandler.cs      # 029 R3 — birebir (TokenTypeIdentifiers URN guard'ıyla)
│   └── SeedHostedService.cs           # idempotent scope+client seed (RBAC bölümü YOK)
├── Data/                              # ApplicationUser + ApplicationDbContext + tek Initial migration
└── (SİLİNİR: Pages/*, ApiKeys/, Data/ApiKey|UserScope, Duende migrations/keys, Duende Config)

src/others/Common/
├── Utils/Constants/AuthorizationScopes.cs   # yeniden yazılır: 6 gateway scope'u
├── Extensions/AuthenticationExtension.cs    # DEĞİŞMEZ (JwtBearer + scope policy; 029 paritesi)
└── (SİLİNİR: Auths/ApiKey*, Extensions/ApiKeyAuthenticationExtension.cs — ölü ECommerce kopyası)

src/services/Payment.Api/              # AddAuthenticationAndAuthorizationExtension + RequireAuthorization
│                                      # (PosAccounts, BinCards, /mcp yüzeyi) + appsettings IdentityOption
src/services/Merchant.Api/             # aynı (Merchants, SettlementAccounts)
src/services/Commission.Api/           # aynı (Banks, BankCommissions, MerchantCommissions)
src/services/Reference.Api/            # DOKUNULMAZ (HTTP yüzeyi yok — event-only)
src/services/gateway/                  # DOKUNULMAZ (AppHost dışı uyur kopya; ayrı temizlik konusu)

src/ui/Admin/                          # AdminTokenHandler (client_credentials, cache'li) + 4 typed client'a
│                                      # AddHttpMessageHandler + appsettings (client id/secret + authority)
src/agents/Payment.Agent/              # MCP client HttpClient'ına token handler + config

src/aspire/AppHost/AppHost.cs          # identityDb + identity-server resource; API/Admin/Agent referansları
Directory.Packages.props               # Duende 3 paket ÇIKAR; OpenIddict 2 paket GİRER
.specify/memory/constitution.md        # İlke V amendment (implement fazında)
```

**Structure Decision**: Mevcut yerleşim korunur; yeni proje açılmaz. Identity.Server yerinde yeniden
kurulur (taşınmaz), BC API'lerde yalnız Program.cs + endpoint extension + appsettings düzeyinde dokunuş.

## Complexity Tracking

> İhlal yok — tablo boş.