# Implementation Plan: Merchant + Commission MCP Yüzeyi Genişletme (Merchant.Agent Söküm)

**Branch**: `043-mcp-surface-expansion` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/043-mcp-surface-expansion/spec.md`

## Summary

Merchant.Api'ye 6 yeni admin-facing MCP tool eklenir (`admin_activate_merchant`/
`admin_deactivate_merchant`/`admin_suspend_merchant`/`admin_get_merchants`/
`admin_approve_registration`/`admin_reject_registration` — mevcut `ChangeStatus`/`Approve`/`Reject`
aggregate metotlarını çağırır). Commission.Api'ye salt-okuma `admin_get_commission_policy` eklenir
(create/update MCP'ye açılmaz; Commission.Api'nin İLK MCP kurulumu). Tool'lar mevcut TEK `/mcp`
endpoint'inde kalır (ikinci endpoint YOK) ama YENİ bir capability scope (`merchant.admin`) ile
tool-bazlı korunur — `ecommerce-onboarding` client'ı (submit_registration'ı çağıran, `merchant.
read/write` taşıyan ama admin OLMAYAN sistem istemcisi) bu scope'u ALMAZ; yalnız `admin-ui` ve
042'de kurulan `external-admin-agent` (Claude Desktop) client'ları alır. Uygulama, PG'de zaten
dormant duran `Common.Utils.Authorization.{RequiredScopeAttribute,ScopeAuthorizationMiddleware}`
(Wolverine) ile tool-bazlı ince yetkiyi aktive eder — `cards.write`/`payment.charge` capability-scope
deseninin (mevcut PG idiomu) devamıdır. `submit_registration` handler'ına başarılı kayıtta admin'e
`mail.delivery` üzerinden bilgilendirici mail publish'i eklenir (dormant altyapı, ilk aktif çağıran).
Merchant.Agent (A2A host) projesi ve Admin "Agent Chat" + "RegisterRequests" Razor sayfaları
sökülür. Payment.Api bu feature'da dokunulmaz.

## Technical Context

**Language/Version**: C# / .NET 10 (mevcut çözüm, `Nullable`+`ImplicitUsings` açık)

**Primary Dependencies**: ASP.NET Core Minimal API, `ModelContextProtocol` SDK (`[McpServerToolType]`,
`MapMcp`), Marten (Postgres document store), Wolverine (`IMessageBus`, `[Transactional]` outbox,
RabbitMQ fanout `mail.delivery`, `opts.Policies.AddMiddleware(typeof(ScopeAuthorizationMiddleware),
chain => chain.MessageType.GetCustomAttribute<RequiredScopeAttribute>() is not null)` — PG'de
`Common`'da zaten var, ilk kez aktive ediliyor), OpenIddict (`Identity.Server/Config.cs` — YENİ
`merchant.admin` capability scope), Scrutor (`AddAllDependencies`, DI marker)

**Storage**: Postgres — `merchantDb` (Marten, `Merchant`+`RegisterRequest` aggregate'leri, şema
değişikliği YOK), `commissionDb` (Marten, `CommissionPolicy`, şema değişikliği YOK). `identityDb`
(OpenIddict) — yeni scope kaydı + iki client'ın (`admin-ui`, `external-admin-agent`) `Permissions`
kümesine `merchant.admin` eklenir (`SeedHostedService` idempotent upsert, migration YOK).

**Testing**: xUnit (`tests/Merchant.Api.Tests`, `tests/Commission.Api.Tests`) — Domain-TDD (İLKE VI/
conventions): yeni MCP tool'ların ARDINDAKİ command handler'ları test-first (zaten var olan
`ChangeStatus`/`Approve`/`Reject` aggregate metotları için ek test gerekmez, mevcutlar kapsıyor);
`ScopeAuthorizationMiddleware`'in doğru scope'u reddettiği/geçirdiği ayrı bir birim testle
doğrulanır (yeni — bu davranış PG'de ilk kez aktive ediliyor); handler/MCP tool ince sarmalayıcı
katmanı canlı doğrulama (test-sonra)

**Target Platform**: .NET Aspire orkestrasyonlu servisler (Merchant.Api, Commission.Api Kestrel
HTTP+MCP Streamable HTTP endpoint — HER İKİSİ tek `/mcp`), dış MCP istemcisi Claude desktop
(`external-admin-agent` OAuth client'ıyla, yerel, ayrı süreç)

**Project Type**: Çoklu-BC web-service çözümü (mevcut); bu feature yeni proje EKLEMEZ, bir proje
SİLER (`src/agents/Merchant.Agent`) + iki Razor sayfası siler (`src/ui/Admin/Pages/AgentChat`,
`src/ui/Admin/Pages/RegisterRequests`)

**Performance Goals**: N/A — düşük hacimli admin operasyonu (manuel onay/statü değişimi), throughput
hedefi yok; mevcut REST endpoint'lerle aynı karakteristik (MCP ince sarmalayıcı, ekstra I/O yok;
scope middleware kontrolü tek claim lookup, ihmal edilebilir)

**Constraints**: Yeni admin tool'lar `merchant.admin` (Merchant.Api) / `commission.read`
(Commission.Api — zaten `ecommerce-onboarding`'de YOK, ek scope gerekmez) scope'unu tool-bazlı
`[RequiredScope]` ile fail-closed uygulamak ZORUNDA; `ecommerce-onboarding` client'ına
`merchant.admin` VERİLMEZ (ayrım budur). Domain invariant (statü makinesi, RegisterRequest
terminal-statü kuralı) MCP yolundan bypass edilemez. `submit_registration`/`registration_status`
tool sözleşmesi (isim/şekil/scope) DEĞİŞMEZ. Payment.Api bu feature kapsamı DIŞINDA.

**Scale/Scope**: Merchant.Api'de 2 yeni Agents klasörü, 6 yeni admin MCP tool, 1 yeni scope
(`merchant.admin`) + 2 client'a (`admin-ui`, `external-admin-agent`) ekleme, Wolverine middleware
aktivasyonu; Commission.Api'de 1 yeni Agents klasörü, 1 yeni admin MCP tool + Commission.Api'nin
İLK MCP kurulumu (`/mcp`, `commission.read`); `Shared/McpToolNames.cs` YENİ dosya (`MerchantAdminTools`,
`CommissionAdminTools` — `admin_` önekli sabitler); 1 proje silme (Merchant.Agent); Admin'de 2 sayfa
silme + ilgili client/route referansları

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **İlke I (BC İzolasyonu)** — PASS. Yeni tool'lar kendi BC'sinin (Merchant.Api/Commission.Api)
  kendi aggregate'ini çağırır; BC-arası doğrudan erişim yok. Merchant.Agent söküm sonrası hiçbir
  servis başka BC'nin DB'sine erişmiyor.
- **İlke II (Zengin Domain)** — PASS. Yeni davranış eklenmiyor; var olan aggregate metotları
  (`ChangeStatus`, `Approve`, `Reject`) olduğu gibi çağrılıyor. Invariant aggregate içinde kalır.
- **İlke III (VSA+CQRS)** — PASS. Yeni MCP tool'lar `Features/Agents/Commands|Queries` altında
  kendi command+handler'ını taşır (conventions: agent slice Features/Commands|Queries'e gitmez,
  kod tekrarı bilinçli); repository yok, `IDocumentSession`/`IMessageBus` doğrudan kullanılır.
- **İlke IV (Result Pattern)** — PASS. Handler'lar `FeatureObjectResultModel<T>`/
  `FeatureResultModel` döner (mevcut `Approve`/`Reject`/`ChangeStatus` zaten `ResultDomain` sarar).
- **İlke V (Merkezi Kimlik+Yetki)** — PASS, GÜÇLENDİRİLDİ. Yeni `merchant.admin` capability scope
  (`cards.write`/`payment.charge` ile aynı desen — G2 istemcisiyle admin istemcisini AYIRAN yeni
  bir yetki katmanı, scope-per-merchant çoğaltması DEĞİL). Her admin tool kendi
  `[RequiredScope(...)]`'unu AÇIKÇA beyan eder (coarse endpoint-policy tek başına yetmezdi —
  `ecommerce-onboarding` da `merchant.write` taşıyor); Wolverine middleware fail-closed uygular.
- **İlke VI (Spec-Driven)** — PASS. Bu plan spec.md'den (clarify tamamlanmış) türüyor.
- **İlke VII (FLOW.md legibility)** — DİKKAT (aksiyon gerektirir, ihlal DEĞİL). Merchant.Api,
  Commission.Api VE Identity.Server FLOW.md'leri domain sürecine yeni adım (statü/onay MCP yolu,
  mail bildirimi, yeni `merchant.admin` scope) ekliyor → **aynı PR'da güncellenmesi ZORUNLU**.
  Merchant.Agent FLOW.md'si proje silinince KALDIRILIR. `scripts/check-flow-links.sh` doğrular.

Gate ihlali YOK; Complexity Tracking tablosu gerekmiyor.

## Project Structure

### Documentation (this feature)

```text
specs/043-mcp-surface-expansion/
├── plan.md              # Bu dosya
├── research.md          # Faz 0 çıktısı
├── data-model.md         # Faz 1 çıktısı
├── quickstart.md         # Faz 1 çıktısı
├── contracts/            # Faz 1 çıktısı (MCP tool sözleşmeleri)
└── tasks.md              # Faz 2 çıktısı (/speckit-tasks — bu komutta ÜRETİLMEZ)
```

### Source Code (repository root)

```text
src/others/Shared/
└── McpToolNames.cs                                   # YENİ dosya — MerchantAdminTools + CommissionAdminTools (admin_ önekli sabitler)

src/others/Common/Utils/Constants/AuthorizationScopes.cs   # DEĞİŞİR — MerchantAdmin = "merchant.admin" eklenir

src/others/Identity.Server/Config.cs                   # DEĞİŞİR — ScopeResources'a merchant.admin (merchant.api audience);
                                                        # admin-ui ve external-admin-agent Scopes listesine merchant.admin eklenir
                                                        # (ecommerce-onboarding DOKUNULMAZ — ayrım budur)

src/services/Merchant.Api/
├── Domains/Merchants/
│   ├── Merchant.cs                                   # DOKUNULMAZ (ChangeStatus zaten var)
│   └── Features/
│       ├── Commands/ChangeMerchantStatus.cs          # DOKUNULMAZ (mevcut REST komutu)
│       └── Agents/
│           ├── Commands/
│           │   ├── AdminActivateMerchant.cs          # YENİ — admin_activate_merchant [RequiredScope(MerchantAdmin)]
│           │   ├── AdminDeactivateMerchant.cs        # YENİ — admin_deactivate_merchant [RequiredScope(MerchantAdmin)]
│           │   └── AdminSuspendMerchant.cs           # YENİ — admin_suspend_merchant [RequiredScope(MerchantAdmin)]
│           └── Queries/
│               └── AdminGetMerchants.cs              # YENİ — admin_get_merchants (liste, opsiyonel statü filtresi) [RequiredScope(MerchantAdmin)]
└── Domains/RegisterRequests/
    ├── RegisterRequest.cs                            # DOKUNULMAZ (Approve/Reject zaten var)
    └── Features/Agents/
        ├── Commands/
        │   ├── SubmitRegistration.cs                 # DEĞİŞİR (FR-011/FR-012: mail publish + mesaj netliği) — scope DEĞİŞMEZ
        │   ├── AdminApproveRegistration.cs           # YENİ — admin_approve_registration [RequiredScope(MerchantAdmin)]
        │   └── AdminRejectRegistration.cs            # YENİ — admin_reject_registration [RequiredScope(MerchantAdmin)]
        └── Queries/
            ├── RegistrationStatus.cs                 # DOKUNULMAZ — scope DEĞİŞMEZ
            └── AdminGetPendingRegistrations.cs       # YENİ — admin_get_pending_registrations [RequiredScope(MerchantAdmin)]

src/services/Merchant.Api/Program.cs                   # DEĞİŞİR — opts.Policies.AddMiddleware(ScopeAuthorizationMiddleware, ...)
                                                        # eklenir (Wolverine opts bloğuna); /mcp endpoint DEĞİŞMEZ (tek uç)

src/services/Commission.Api/
├── Domains/CommissionPolicies/
│   ├── CommissionPolicy.cs                            # DOKUNULMAZ
│   └── Features/Agents/Queries/                       # YENİ klasör
│       └── AdminGetCommissionPolicy.cs                # YENİ — admin_get_commission_policy
└── Program.cs                                          # DEĞİŞİR — İLK MCP kurulumu: AddMcpServer().WithHttpTransport()
                                                        # .WithToolsFromAssembly() + app.MapMcp("/mcp").RequireAuthorization(CommissionRead)
                                                        # (ecommerce-onboarding'de commission scope hiç yok — ek middleware gerekmez)

src/agents/Merchant.Agent/                              # SİLİNİR (tüm proje)

src/ui/Admin/
├── Pages/AgentChat/                                    # SİLİNİR
├── Pages/RegisterRequests/                              # SİLİNİR
├── Clients/MerchantAgentClient.cs                       # SİLİNİR
└── Program.cs                                           # DEĞİŞİR (Merchant.Agent referansı/route kaldırılır)

src/aspire/AppHost/AppHost.cs                            # DEĞİŞİR (Merchant.Agent kaydı kaldırılır)

tests/Merchant.Api.Tests/                                # DEĞİŞİR (yeni Agents slice'ları + ScopeAuthorizationMiddleware testi)
tests/Commission.Api.Tests/                              # DEĞİŞİR (admin_get_commission_policy handler testi)
```

**Structure Decision**: Mevcut çoklu-BC .NET çözümü korunur (yeni proje yok, yeni endpoint yok).
Değişiklik iki BC'nin (`Merchant.Api`, `Commission.Api`) VSA klasör yapısına yeni `Features/
Agents/*` slice'ları eklemek + Identity.Server'a yeni bir capability scope (`merchant.admin`)
tanımlamak + bir projeyi (`Merchant.Agent`) ve iki Admin Razor sayfasını (`AgentChat`,
`RegisterRequests`) tamamen kaldırmakla sınırlı. Payment.Api dokunulmaz.

## Complexity Tracking

*Gate ihlali yok — bu tablo boş.*