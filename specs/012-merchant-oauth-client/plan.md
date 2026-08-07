# Implementation Plan: Merchant OAuth İstemci Düzlemi (G2 — Makine Kimliği)

**Branch**: `012-merchant-oauth-client` | **Date**: 2026-08-07 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/012-merchant-oauth-client/spec.md`

## Summary

Merchant'lar OpenIddict'te client_credentials istemcisi olur (`client_id=merchantId`,
`client_secret=MerchantKey`). Merchant BC yaşam döngüsü olayları (MerchantCreated /
MerchantStatusChanged, Shared fanout) Identity.Server'daki yeni Wolverine tüketicisini
besler; tüketici OpenIddict application store'unda istemci kaydını yaratır/pasifler
(status-gated issuance). Token 15 dk ömürlü, `merchant_id` claim'i + mevcut
`merchant.read`/`merchant.write` scope'ları taşır. Merchant BC uçlarına Common'da
kurulan claim-vs-route enforcement policy'si eklenir: `merchant_id` claim'i path'teki
`merchantId` ile eşleşmezse 403; claim'siz token'lar (admin-ui, payment-agent) mevcut
davranışını korur.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, Nullable + ImplicitUsings açık)

**Primary Dependencies**: OpenIddict 7.6 (AspNetCore + EntityFrameworkCore),
Wolverine + WolverineFx.RabbitMQ (Identity.Server'a YENİ eklenir — CPM'de sürüm mevcut),
Marten (Merchant BC — değişmez), ASP.NET Core Minimal API + JwtBearer

**Storage**: Identity.Server → EF Core/`identityDb` (OpenIddict application tablosu;
anayasanın izole-altyapı istisnası). Merchant BC → Marten/`merchant_schema` (değişiklik yok)

**Testing**: Saf domain birim testleri (`tests/Merchant.Api.Tests` mevcut xUnit projesi);
enforcement karar mantığı saf statik fonksiyona çıkarılıp Common üstünden test edilir.
Handler/HTTP entegrasyonu quickstart canlı senaryolarıyla elle doğrulanır (proje konvansiyonu)

**Target Platform**: Aspire AppHost orkestasyonu (Postgres + RabbitMQ + Identity.Server
5101 + 3 BC API + Admin BFF + Payment.Agent)

**Project Type**: Mikroservis (mevcut çözüm içi değişiklik — yeni proje YOK)

**Performance Goals**: Belirgin hedef yok; token ucu mevcut OpenIddict akışı, event
tüketimi düşük hacim (onboarding/status değişimi nadir olay)

**Constraints**: MerchantKey yalnız `connect/token`'a gider; self-contained JWT (introspection
yok); token ömrü 15 dk GLOBAL (bkz. research D5 — admin/agent handler'ları proaktif
yenilediği için davranış değişmez); backfill yok (dev fazı, Docker reset)

**Scale/Scope**: 2 yeni integration event, Identity.Server'a 1 tüketici + claim genişletmesi,
Merchant BC'ye 1 yeni slice (SetMerchantStatus) + 1 event publish, Common'a 2 policy +
1 authorization handler, ~9 uca policy eklenmesi

## Constitution Check

*GATE: v1.2.0'a göre değerlendirildi — Phase 0 öncesi GEÇTİ, Phase 1 sonrası yeniden doğrulandı.*

| İlke | Değerlendirme | Sonuç |
|------|---------------|-------|
| I. BC İzolasyonu | Identity.Server BC değil, altyapı servisi (011 istisnası sürüyor). Merchant→Identity iletişimi YALNIZ Shared integration event'leriyle; Identity, Merchant DB'sine dokunmaz, kendi izdüşümünü (OpenIddict application kaydı) tutar. DB/model paylaşımı yok. | ✅ |
| II. Zengin Domain | Merchant aggregate'inin mevcut `Activate/Deactivate/Suspend` metotları kullanılır; invariant aggregate'te kalır. Yeni anemik model yok. | ✅ |
| III. Vertical Slice + CQRS | Yeni `SetMerchantStatus` slice'ı standart desende (static class + record command + Handler + endpoint, `[Transactional]`, `IDocumentSession`). Identity.Server slice deseni dışında (altyapı servisi — 011 emsali). | ✅ |
| IV. Result Pattern | Slice'lar `FeatureObjectResultModel<T>`/`ResultDomain` döner; OpenIddict hata yüzeyi kendi standardında (011'de kabul edilen sapma). | ✅ |
| V. Merkezi Kimlik ve Açık Yetki | Bu feature İlke V'in G2 TODO'sunu KAPATIR: merchant-istemci düzlemi karara bağlanır → implement sonrası anayasa amendment'ı gerekir (TODO(AUTHZ_MODEL) daraltma, MINOR bump). Her uç policy'sini açıkça beyan etmeye devam eder; multitenant izolasyon ("merchant verisi sızmaz") ilk kez mekanizmaya kavuşur. | ✅ (amendment görevi tasks'a) |
| VI. Spec-Driven | Tam akış izleniyor (spec → plan → tasks → implement). | ✅ |
| Teknoloji kısıtları | Wolverine/RabbitMQ mesajlaşma ✓, CPM (WolverineFx.RabbitMQ sürümü zaten `Directory.Packages.props`'ta) ✓, DI marker ✓, Türkçe mesajlar ✓. | ✅ |

Gate ihlali yok → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/012-merchant-oauth-client/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — D1..D9 kararları
├── data-model.md        # Phase 1 — varlıklar, olaylar, durum geçişleri
├── quickstart.md        # Phase 1 — canlı doğrulama senaryoları S1..S7
├── contracts/
│   ├── merchant-token.md        # connect/token sözleşmesi (merchant istemcisi)
│   ├── integration-events.md    # MerchantCreated / MerchantStatusChanged + exchange/queue
│   └── enforcement.md           # uç-başına policy matrisi
└── tasks.md             # Phase 2 (/speckit-tasks üretir — bu komut değil)
```

### Source Code (repository root)

```text
src/others/Shared/
├── IntegrationEvents.cs             # + MerchantCreated, MerchantStatusChanged
└── RabbitMqConstants.cs             # + MerchantLifecycleExchange, IdentityMerchantSyncQueue

src/others/Common/
├── Utils/Constants/AuthorizationPolicies.cs      # YENİ: MerchantScoped, AdminPlaneOnly
├── Utils/Authorization/MerchantScopeEvaluator.cs # YENİ: saf karar fonksiyonu (test edilir)
├── Utils/Authorization/MerchantScopeRequirement.cs + Handler  # YENİ: IAuthorizationRequirement
└── Extensions/AuthenticationExtension.cs         # policy + handler + IHttpContextAccessor kaydı

src/services/Merchant.Api/
├── Domains/Merchants/Features/Commands/CreateMerchant.cs     # + MerchantCreated publish
├── Domains/Merchants/Features/Commands/SetMerchantStatus.cs  # YENİ slice + endpoint + event
├── Domains/Merchants/Features/Queries/GetMerchant.cs         # route {id}→{merchantId} + MerchantScoped
├── Domains/Merchants/Features/Queries/{GetAllMerchants,GetMerchantByKey}.cs  # + MerchantScoped (fail-closed)
├── Domains/SettlementAccounts/Features/**                    # 5 uca + MerchantScoped
└── Program.cs                                                # + merchant.lifecycle fanout publisher

src/others/Identity.Server/
├── Program.cs                        # + UseWolverine (RabbitMQ listener) + SetAccessTokenLifetime(15dk)
├── MerchantClientEventHandlers.cs    # YENİ: tüketici → IOpenIddictApplicationManager (idempotent)
└── Connect/TokenEndpoint.cs          # + merchant_id claim'i (application Properties'ten)

tests/Merchant.Api.Tests/             # + MerchantScopeEvaluator birim testleri (Common'a referans mevcut)
```

**Structure Decision**: Yeni proje açılmaz. Değişiklik dört mevcut alana dağılır:
Shared (kontratlar), Common (enforcement altyapısı — G3 yeniden kullanacak),
Merchant.Api (publish + yeni slice + policy beyanları), Identity.Server (tüketici +
claim + ömür). Aspire AppHost değişikliği: Identity.Server'a RabbitMQ referansı eklenir.

## Complexity Tracking

> İhlal yok — boş.