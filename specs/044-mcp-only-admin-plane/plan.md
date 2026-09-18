# Implementation Plan: MCP-Only Admin Düzlemi

**Branch**: `044-mcp-only-admin-plane` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/044-mcp-only-admin-plane/spec.md`

## Summary

Admin operasyonlarının tek arayüzü MCP (Claude desktop) olur: Merchant.Api'ye
`admin_get_merchant` + `admin_update_merchant` (hassas-dışı alanlar), Commission.Api'ye
`admin_create_commission_policy` + `admin_update_commission_margin` +
`admin_change_commission_status` tool'ları eklenir. Mevcut
`admin_get_merchants` / `admin_get_pending_registrations` yanıtlarından Email+GsmNumber kırpılır.
Hassas alanlar (Email, GSM, TCKN, IBAN, TaxNumber) yalnız Admin UI'daki tek hassas-veri
sayfasından (dar BFF ucu, AdminPlaneOnly) yönetilir. Admin-düzlemi REST uçları + Admin CRUD
sayfaları + `CreateMerchant` slice + ölü OAuth client'ları (`merchant-agent`, `payment-agent`)
sökülür; MerchantScoped uçlar (tekil merchant, kendi komisyon politikası) DOKUNULMAZ.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: ASP.NET Core Minimal API, Wolverine (+`[Transactional]` outbox),
Marten/PostgreSQL, ModelContextProtocol (MCP server, `WithToolsFromAssembly` + `MapMcp`),
OpenIddict (Identity.Server), Razor Pages (Admin BFF), Aspire AppHost

**Storage**: merchantDb + commissionDb (Marten, BC-başına ayrı DB; şema değişikliği YOK)

**Testing**: xUnit (`tests/Merchant.Api.Tests`, `tests/Commission.Api.Tests`); saf domain
değişikliği yok → domain-TDD tetiklenmez (İLKE VI kapsamı); handler/tool testleri test-sonra

**Target Platform**: Aspire orkestrasyonlu yerel geliştirme (macOS/Linux); canlı doğrulama
Claude desktop MCP istemcisiyle

**Project Type**: Mikroservis (BC-başına servis) + Razor BFF

**Performance Goals**: Yerel admin operasyonu; özel hedef yok (tek admin kullanıcı)

**Constraints**: Hassas alanlar hiçbir MCP sözleşmesinde (girdi/çıktı) yer alamaz; söküm yeni
yüzey canlı doğrulanmadan başlayamaz; MerchantScoped uçlar ve makine kanalları değişmez

**Scale/Scope**: 5 yeni MCP tool, 2 tool yanıtı kırpma, 1 yeni Admin sayfası + 2 dar BFF ucu,
~10 REST ucu + 3 Admin sayfası/3 istemci + 2 slice (CreateMerchant, CalculateEffectiveCommission)
+ 2 ölü OAuth client sökümü

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **İLKE I (BC izolasyonu)**: GEÇER — BC'ler arası yeni kanal yok; her tool kendi BC'sinin
  slice'ını çağırır. Söküm BC sınırlarını değiştirmez.
- **İLKE II (Zengin aggregate)**: GEÇER — aggregate değişikliği yok; mevcut `UpdateDetails`
  hassas+hassas-dışı alanları birlikte alır → handler mevcut değerleri koruyarak çağırır
  (bkz. research.md R3); yeni aggregate metodu GEREKMEZ.
- **İLKE III (VSA+CQRS)**: GEÇER — yeni tool'lar `Features/Agents/Commands|Queries/` slice'ları;
  MCP wrapper slice dosyasında; Commands/Queries'e IMessageBus ile bile gidilmez (bilinçli tekrar).
- **İLKE IV (Result)**: GEÇER — tüm yeni handler'lar `FeatureObjectResultModel<T>` döner.
- **İLKE V (Açık yetki)**: GEÇER — merchant tool'ları `[RequiredScope(MerchantAdmin)]` +
  Wolverine middleware (043 deseni); commission yazma tool'ları `[RequiredScope(CommissionWrite)]`
  + middleware'in Commission.Api'ye taşınması; BFF ucu `MerchantWrite`+`AdminPlaneOnly`.
- **İLKE VI (Spec-driven)**: GEÇER — tam akış (yeni sözleşmeler + söküm = tam kademe).
- **İLKE VII (FLOW.md)**: TETİKLENİR — Merchant/Commission süreçlerine yeni admin adımları,
  CreateMerchant yolunun silinmesi; FLOW.md'ler aynı iş içinde güncellenir.

## Project Structure

### Documentation (this feature)

```text
specs/044-mcp-only-admin-plane/
├── plan.md              # Bu dosya
├── research.md          # Faz 0 kararları
├── data-model.md        # Alan sınıflandırması + sözleşme değişimleri
├── quickstart.md        # Canlı doğrulama senaryoları
├── contracts/
│   ├── merchant-admin-mcp-tools.md
│   ├── commission-admin-mcp-tools.md
│   └── admin-sensitive-endpoint.md
└── tasks.md             # /speckit-tasks üretir (bu komut DEĞİL)
```

### Source Code (repository root)

```text
src/services/Merchant.Api/
├── Domains/Merchants/
│   ├── Features/Agents/Queries/AdminGetMerchant.cs        # YENİ (tool: admin_get_merchant)
│   ├── Features/Agents/Commands/AdminUpdateMerchant.cs    # YENİ (tool: admin_update_merchant)
│   ├── Features/Agents/Queries/AdminGetMerchants.cs       # DEĞİŞİR (Email/Gsm kırpılır)
│   ├── Features/Queries/GetMerchantSensitive.cs           # YENİ (BFF, AdminPlaneOnly)
│   ├── Features/Commands/UpdateMerchantSensitive.cs       # YENİ (BFF, AdminPlaneOnly)
│   ├── Features/Commands/CreateMerchant.cs                # SİLİNİR
│   ├── Features/Commands/UpdateMerchant.cs                # SİLİNİR
│   ├── Features/Commands/ChangeMerchantStatus.cs          # SİLİNİR
│   ├── Features/Queries/ListMerchants.cs                  # SİLİNİR
│   ├── Features/Queries/GetMerchant.cs                    # KALIR (MerchantScoped)
│   └── MerchantEndpointExtension.cs                       # DEĞİŞİR (yalnız Get + sensitive çifti)
├── Domains/RegisterRequests/
│   ├── Features/Agents/Queries/AdminGetPendingRegistrations.cs  # DEĞİŞİR (Email/Gsm kırpılır)
│   ├── Features/Commands/{Approve,Reject}RegisterRequest.cs     # SİLİNİR (REST; MCP muadili var)
│   ├── Features/Queries/ListRegisterRequests.cs                 # SİLİNİR
│   └── RegisterRequestEndpointExtension.cs                      # SİLİNİR (redeem ayrı grupta)
└── FLOW.md                                                      # GÜNCELLENİR

src/services/Commission.Api/
├── Domains/CommissionPolicies/
│   ├── Features/Agents/Commands/AdminCreateCommissionPolicy.cs   # YENİ
│   ├── Features/Agents/Commands/AdminUpdateCommissionMargin.cs   # YENİ
│   ├── Features/Agents/Commands/AdminChangeCommissionStatus.cs   # YENİ
│   ├── Features/Commands/*.cs                                    # SİLİNİR (3 REST slice)
│   ├── Features/Queries/{ListCommissionPolicies,CalculateEffectiveCommission}.cs  # SİLİNİR
│   ├── Features/Queries/GetCommissionPolicy.cs                   # KALIR (MerchantScoped)
│   └── CommissionPolicyEndpointExtension.cs                      # DEĞİŞİR (yalnız Get)
├── Auth/ScopeAuthorizationMiddleware*                            # YENİ (043 deseninin taşınması)
└── FLOW.md                                                       # GÜNCELLENİR

src/ui/Admin/
├── Pages/Merchants/Sensitive.cshtml(.cs)     # YENİ (tek hassas-veri sayfası)
├── Pages/Merchants/{Index,Details}.*         # SİLİNİR
├── Pages/CommissionPolicies/                 # SİLİNİR
├── Clients/MerchantApiClient.cs              # DARALIR (yalnız sensitive get/put)
├── Clients/{CommissionPolicyApiClient,RegisterRequestApiClient}.cs  # SİLİNİR
└── Pages/Shared/_Layout.cshtml               # DEĞİŞİR (menü)

src/others/Identity.Server/Config.cs          # DEĞİŞİR (merchant-agent + payment-agent ölü
                                              # client seed'leri silinir); FLOW.md güncellenir
src/others/Shared/McpToolNames.cs             # DEĞİŞİR (yeni tool adı sabitleri)

tests/Merchant.Api.Tests, tests/Commission.Api.Tests  # yeni slice testleri + kırpma regresyonu
```

**Structure Decision**: Mevcut VSA yerleşimi korunur; yeni agent slice'ları `Features/Agents/
Commands|Queries` altına, MCP wrapper aynı dosyaya. BFF hassas uçları normal `Features/
Commands|Queries` slice'ı (kullanıcı=admin tetikler, Domains kuralı).

## Complexity Tracking

> Anayasa ihlali yok — tablo boş.