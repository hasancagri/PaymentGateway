# Implementation Plan: Tek MCP Yüzeyinde PG — Store Fasadına Bağlanma + Platform Kimliği

**Branch**: `047-platform-single-mcp` | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/047-platform-single-mcp/spec.md`

## Summary

PG'nin Merchant/Commission MCP tool'ları (14 tool) ECommerce Mcp.Gateway fasadına downstream bağlanır;
PG MCP yüzeyinin kimlik otoritesi AgentPlatform IdP'ye geçer (yüzey başına tek otorite). Platform
AppRegistry'ye ikinci app (`pg`) config'le kaydedilir; tool budaması EC'nin 085 mekanizmasının PG'ye
bilinçli kopyasıyla yapılır. Payment.Api tool'suz `/mcp` + PG gateway MCP rota kalıntıları +
`external-admin-agent` sökülür. Üç repo, üç PR; sıra: AgentPlatform → ECommerce → PG.

## Technical Context

**Language/Version**: C# / .NET 10 (üç repo da)

**Primary Dependencies**: ModelContextProtocol (AddMcpServer/MapMcp), OpenIddict (AgentPlatform IdP),
Wolverine (ScopeAuthorizationMiddleware), YARP (PG gateway), Aspire (iki ayrı AppHost)

**Storage**: değişmez — yeni tablo/aggregate YOK (config + kimlik düzlemi işi)

**Testing**: xUnit saf birim (İLKE VI: budama kuralı test-first); endpoint/kimlik canlı doğrulama (quickstart)

**Target Platform**: localhost dev — PG Aspire (Merchant 5202 / Commission 5203 / Payment 5201, IdP 5101),
EC Aspire (fasat + BC'ler), AgentPlatform IdP 5001

**Project Type**: mikroservis / çok-repo entegrasyon feature'ı

**Performance Goals**: fasat keşif cache'i mevcut TTL'iyle; ek hedef yok

**Constraints**: yüzey başına tek otorite (MCP=platform, REST=PG IdP); ödeme yolu + merchant istemci
düzlemi DAVRANIŞ DEĞİŞTİRMEZ; scope adları platformda app'ler arası benzersiz (validator zorlar)

**Scale/Scope**: PG 14 tool + 5 scope; 3 repo'da ~10 dosya değişikliği + söküm

## Constitution Check

*GATE: geçti (v1.5.0). Post-design yeniden değerlendirme: ihlal yok.*

- **İLKE I (BC izolasyonu)**: BC'ler arası yeni kanal yok — fasat MCP'si agent istemcisine hizmet eden
  sanksiyonlu yüzey; PG BC'leri birbirinin DB/modeline dokunmuyor. Budama yardımcısı Common'da (altyapı
  katmanı, domain değil). PASS.
- **İLKE II/III/IV**: yeni aggregate/slice yok; mevcut Agents slice'ları ve Result akışı değişmiyor. PASS.
- **İLKE V (merkezi kimlik + açık yetki)**: her uç policy'sini açıkça beyan etmeye devam eder; MCP yüzeyi
  yalnız platform token'ı (fail-closed ayrım), REST PG IdP'de. Amendment GEREKMEZ: anayasa "kimlik
  merkezî Identity.Server" der; insan/agent düzleminin platform IdP'sine bağlanması İlke V'in makine/
  merchant hükümlerini değiştirmiyor — ama issuer cümlesi 5101'i sabitliyor → **plan notu: anayasaya
  MINOR amendment (insan/agent düzlemi platform IdP'si) implement PR'ında işlenir**. Bkz. Complexity.
- **İLKE VI (spec-driven + domain-TDD)**: tam akış izleniyor; saf budama kuralı test-first. PASS.
- **İLKE VII (FLOW.md)**: domain süreci DEĞİŞMİYOR (transport/kimlik işi) — FLOW güncellemesi
  gerekmez; guard script'leri regresyon turunda koşulur. PASS.

## Project Structure

### Documentation (this feature)

```text
specs/047-platform-single-mcp/
├── plan.md              # bu dosya
├── research.md          # Phase 0 kararları (R1-R8)
├── data-model.md        # Phase 1 — config/kayıt varlıkları (aggregate yok)
├── quickstart.md        # Phase 1 — canlı doğrulama senaryoları S1-S5
├── contracts/
│   └── mcp-surface.md   # fasat-PG downstream + scope bölmesi kontratı
└── tasks.md             # /speckit-tasks üretir
```

### Source Code (repository root + kardeş repolar)

```text
PaymentGateway (bu repo):
src/others/Common/
├── Extensions/AuthenticationExtension.cs      # + AddPlatformMcpAuthentication (ikinci JwtBearer şeması)
├── Extensions/McpScopePruningExtension.cs     # YENİ — EC 085 kopyası (bilinçli tekrar)
└── Options/PlatformIdentityOption.cs          # YENİ — platform IdP adres/audience POCO
src/services/Merchant.Api/
├── Program.cs                                 # /mcp → Platform şeması + scope budama; Mcp/ holder
└── Mcp/MerchantAdminSurface.cs                # YENİ — tool→scope haritası (10 tool)
src/services/Commission.Api/
├── Program.cs                                 # aynı desen
└── Mcp/CommissionAdminSurface.cs              # YENİ — tool→scope haritası (4 tool)
src/services/Payment.Api/Program.cs            # AddMcpServer + MapMcp SÖKÜM
src/services/gateway/Gateway/appsettings*.json # *-mcp-route kalıntıları SÖKÜM
src/others/Identity.Server/                    # Config.cs seed söküm + RetiredClientIds + AdminAgentApplicationManager söküm
scripts/claude-desktop-pg-mcp.sh               # SİL
tests/Merchant.Api.Tests/ (veya Common testi)  # ScopePruningTests — test-first

ECommerceWithAgentFramework (kardeş):
src/agents/Mcp.Gateway/FacadeScopes.cs         # All += PG 5 scope
src/agents/Mcp.Gateway/appsettings*.json       # Downstreams += pg-merchant/pg-commission (sabit URL); DiscoveryScope += PG
src/others/Common/.../AuthorizationScopes.cs   # PG scope sabitleri (fasat ilanı için)

AgentPlatform (kardeş):
src/Identity.Server/appsettings*.json          # AppRegistry.Apps += "pg" app (scope bölmesi + admin rol katkısı);
                                               # external-admin-agent + mcp-gateway-discovery Scopes += PG demeti
```

**Structure Decision**: PG ana repo; kardeş repolarda config-ağırlıklı küçük PR'lar. AgentPlatform
tarafı SALT CONFIG (AppRegistry 084'te tam bunun için kuruldu — kod değişikliği beklenmiyor).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Anayasa V issuer cümlesi (sabit 5101) insan/agent düzleminde platform IdP'yle genişliyor | Tek login + tek MCP kaydı emeli; insan düzlemi PG'de hiç var olmadı | Amendment'sız bırakmak: spec/anayasa çelişir; MINOR amendment implement PR'ında işlenecek |
| Budama yardımcısının EC'den PG Common'a kopyası | İki repo bağımsız evrilir; paylaşılan lib İLKE I'e aykırı bağ kurar | NuGet/ortak paket: tek tüketici çifti için YAGNI + repo bağlaşması (bilinçli tekrar kuralı) |
