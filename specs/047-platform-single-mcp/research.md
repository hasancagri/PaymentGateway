# Research: 047 Tek MCP Yüzeyinde PG

Kaynaklar koddan doğrulandı (2026-09-26): PG `Config.cs`/`AuthenticationExtension.cs`/Program.cs'ler,
EC `ToolCatalogCollector`/`McpScopePruningExtension`/`FacadeScopes`, AgentPlatform `AppRegistry*`.

## R1 — Platform kaydı: AppRegistry ikinci app (salt config)

- **Decision**: `AppRegistry.Apps`'e `AppId: "pg"` girişi: scope bölmesi (aşağıda R2), `Roles`
  katkısı (`admin` rolüne PG demeti — additive birleşim 084'te hazır), `Clients` BOŞ (PG'ye özel
  yeni istemci yok). EC app'indeki `external-admin-agent` + `mcp-gateway-discovery` Scopes listesine
  PG scope'ları eklenir (tavan genişler; scope'lar union'dan doğrulanır).
- **Rationale**: AppRegistry 084'te tam bu senaryo için kuruldu ("yeni app'ler için 'pg'" yorumu
  kodda). Kod değişikliği YOK, yalnız appsettings — platform generic kalır.
- **Alternatives**: PG'ye ayrı public istemci (ikinci Claude kaydı) — tek-kayıt emeline aykırı, RET.

## R2 — Scope adları: PREFIX'SİZ, PG adları aynen

- **Decision**: Platforma giren PG bölmesi: `merchant.read/write/admin` (audience `merchant.api`),
  `commission.read/write` (audience `commission.api`). Adlar PG kodundaki sabitlerle BİREBİR;
  `ScopeNamespace.Prefix = "pg"` alanı doldurulur ama ad üretimine karışmaz (validator yalnız
  app'ler-arası ad benzersizliği zorlar — kesişim bugün 0).
- **Rationale**: PG'nin Wolverine `[RequiredScope]` + endpoint policy sabitleri DEĞİŞMEZ; `payment.*`
  çarpışması yok çünkü payment scope'ları platforma GİRMİYOR (Payment MCP'siz).
- **Alternatives**: `pg.merchant.admin` gibi prefixli adlar — PG'de MCP/REST için çift scope sabiti
  doğurur (aynı yetkinin iki adı), karmaşıklık; çakışma çıkarsa gelecekte devreye alınır.

## R3 — PG çift authority: ikinci JwtBearer şeması, yüzey başına tek otorite

- **Decision**: PG Common'a `PlatformIdentityOption` POCO + `AuthenticationExtension`'a ek: `"Platform"`
  adlı ikinci JwtBearer şeması (Authority=platform IdP, Audience=BC'nin kendi adı, MapInboundClaims=false).
  MCP için ayrı policy'ler: `AddAuthenticationSchemes("Platform")` + `RequireClaim("scope", ...)` —
  `MapMcp("/mcp")` yalnız bu policy'yi kullanır. REST policy'leri default şemada (PG IdP) kalır →
  FR-005 (PG token MCP'ye giremez, platform token REST'e giremez).
- **Rationale**: .NET çoklu şema doğal; policy şema kısıtı fail-closed ayrımı tek yerde kurar.
  Wolverine `ScopeAuthorizationMiddleware` ClaimsPrincipal okur — şemadan bağımsız çalışır.
- **Alternatives**: Tek şemada çift issuer (TokenValidationParameters.ValidIssuers) — yüzey ayrımı
  kaybolur (her token her yüzeye girer), İLKE V fail-closed ruhuna aykırı, RET.

## R4 — Tool budaması: EC 085 deseninin PG kopyası (bilinçli tekrar)

- **Decision**: `McpScopePruningExtension.IsToolVisible` PG Common'a kopyalanır; BC başına holder:
  `Merchant.Api/Mcp/MerchantAdminSurface.ToolScopeMap` (10 tool → `[RequiredScope]`'la birebir),
  `Commission.Api/Mcp/CommissionAdminSurface.ToolScopeMap` (4 tool). Program.cs
  `WithHttpTransport(ConfigureSessionOptions=...)` ile oturum token'ına göre süzer (Catalog.Api emsali).
- **Rationale**: Fasat süzme YAPMAZ (085 mimarisi: her downstream kendi budanmış listesini döner);
  PG downstream olunca aynı sözleşmeyi karşılamalı. Kopya = conventions "bilinçli tekrar" maddesi.
- **Alternatives**: Ortak NuGet/link dosyası — repo bağlaşması, YAGNI, RET.

## R5 — Fasat tarafı: downstream + PRM ilanı + keşif

- **Decision**: EC `FacadeOption.Downstreams` += `pg-merchant http://localhost:5202/mcp`,
  `pg-commission http://localhost:5203/mcp` (SABİT URL — PG ayrı Aspire, service discovery yok;
  launchSettings portları sabit). `FacadeScopes.All` += PG 5 scope (PRM `scopes_supported` ilanı —
  079 tuzağı: ilan edilmeyen scope talep edilmez → audience eksik → 401). `DiscoveryScope` += aynı
  demet (m2m tam-katalog registry'si PG tool'larını görsün).
- **Rationale**: `ToolCatalogCollector` oturum token'ını downstream'e taşır; PG budanmış liste döner —
  fasatta PG'ye özel kod YOK, yalnız config + sabit listesi.
- **Alternatives**: PG'ye ayrı mini-fasat — ekstra hop/servis, kullanıcı "EC mantığı" dedi (BC /mcp
  iç tesisat, tek dış yüzey), RET.

## R6 — Payment.Api /mcp + PG gateway MCP rotaları söküm

- **Decision**: Payment Program.cs'ten `AddMcpServer()...WithToolsFromAssembly()` bloğu + `MapMcp`
  satırı silinir (tool 0; `payment.write` REST policy'leri kalır). PG gateway `appsettings*.json`
  `*-mcp-route` girişleri silinir — hepsi EC kalıntısı (basket/catalog/order... PG'de yok); MCP-dışı
  bayat rotaların genel temizliği AYRI iş (spec edge notu).
- **Rationale**: FR-010 "tool taşımayan MCP ucu kalmaz"; kullanıcı kararı "PG için tek MCP".

## R7 — external-admin-agent söküm (PG IdP)

- **Decision**: PG `Config.cs`: seed listesinden çıkar; `RetiredClientIds` += `external-admin-agent`
  (044 prune mekanizması store'dan siler — fail-closed); `AdminAgentApplicationManager` (loopback
  muafiyeti yalnız bu istemci içindi) + `ClaudeCallbackRedirectUris` sabiti sökülür;
  `scripts/claude-desktop-pg-mcp.sh` silinir.
- **Rationale**: Ölü ama yetkili kimlik yüzeyi bırakılmaz (044 R5 emsali).

## R8 — Canlı doğrulama düzeni + PR sırası

- **Decision**: Üç sistem birlikte: AgentPlatform IdP + EC Aspire + PG Aspire. PR sırası:
  (1) AgentPlatform config PR — merge ÖNCE (085 T007 emsali); (2) EC fasat PR; (3) PG ana PR.
  Claude Desktop'ta pg-* kayıtları silinip TEK store kaydıyla S1-S5 koşulur (quickstart).
- **Rationale**: Fasat PRM ilanı platform scope kaydına bağımlı; PG canlı doğrulaması ikisine bağımlı.
- **Risk notu**: platform token'ının scope claim biçimi (JSON dizi) PG policy'leriyle canlıda teyit
  edilir (`ScopeClaimArrayHandler` tuzağı — CLAUDE.md).
