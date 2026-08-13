# Implementation Plan: Merchant SubMerchant Model

**Branch**: `023-merchant-submerchant-model` | **Date**: 2026-08-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/023-merchant-submerchant-model/spec.md`

## Summary

Merchant BC'yi iyzico SubMerchant (pazaryeri) sözleşmesiyle hizalı yeni **Merchant**
aggregate'iyle sıfırdan kurar: zengin aggregate (tip-uyum + IBAN/e-posta doğrulaması +
statü makinesi + MerchantKey üretimi), vertical slice CRUD + statü ucu (scope + düzlem
policy'li), 012 Identity zincirinin yeniden bağlanması (`merchant.lifecycle` →
OpenIddict istemci senkronu — mevcut sözleşme ve tüketici DEĞİŞMEZ) ve saf domain birim
testlerinin çözüme geri dönüşü. İyzico'ya gerçek ağ çağrısı YOK; 022'nin
`Domains/SubMerchants/` + `Provider/` malzemesi hammadde olarak yerinde kalır.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten (Postgres document store, `IntegrateWithWolverine`),
Wolverine (`[Transactional]` outbox + RabbitMQ fanout), Minimal API + Asp.Versioning,
OpenIddict (Identity.Server — değişmez), Aspire (AppHost orkestrasyon)

**Storage**: Postgres via Marten — `merchantDb`, şema `SchemaConstants.MerchantSchemaName`;
Identity.Server kendi `identityDb`'si (EF Core, değişmez)

**Testing**: xUnit (`tests/Merchant.Api.Tests` — 022'de silinen projenin csproj deseniyle
geri gelir; CPM sürümleri `Directory.Packages.props`'ta duruyor: xunit 2.9.3 + runner)

**Target Platform**: Aspire AppHost üstünde koşan mikroservis seti (dev: macOS/localhost)

**Project Type**: Web service (BC API) + saf domain test projesi

**Performance Goals**: Dev fazı — özel hedef yok; liste ucu tam-liste (sayfalama yok, spec varsayımı)

**Constraints**: MerchantKey yalnız oluşturma yanıtında bir kez döner (SC-004); kayıt +
duyuru atomik ([Transactional] outbox — FR-005); sağlayıcı (Provider/SubMerchants) tipleri
BC dışına sızmaz; yalnız TL (para birimi alanı modellenmez)

**Scale/Scope**: 1 yeni aggregate, 5 slice (3 command + 2 query), 1 test projesi,
mevcut 2 integration event yeniden kullanılır; Identity.Server/Shared/Admin'e dokunulmaz

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Durum | Not |
|------|-------|-----|
| I. BC İzolasyonu | ✅ | Yeni model yalnız Merchant BC içinde; Identity ile iletişim mevcut `Shared.IntegrationEvents` kontratları (MerchantCreated/MerchantStatusChanged) + RabbitMQ fanout. DB/model paylaşımı yok. |
| II. Zengin Domain | ✅ | `Merchant : AggregateRoot`, private setter + statik `Create` fabrikası + davranış metotları; invariant'lar (tip-uyum, IBAN, e-posta, statü) aggregate içinde. NOT: Anayasa II'deki "BaseModel/Enumeration" ifadeleri 2026-08-11 refactor'üyle tarihî (PATCH amendment bekliyor — CLAUDE.md "Bilinçli ertelemeler"); güncel kural: AggregateRoot tek base + düz enum. |
| III. Vertical Slice + CQRS | ✅ | `Domains/Merchants/Features/{Commands,Queries}` — bir feature = bir static class (record + Response + Handler + endpoint extension); repository yok, `IDocumentSession` doğrudan. |
| IV. Result Pattern | ✅ | Aggregate metotları `ResultDomain`/`ResultDomain<T>` (014 sözleşmesi, void mutator dahil); handler'lar `FeatureObjectResultModel<T>`; `MessageItem.Code` resource sabiti. |
| V. Merkezi Kimlik + Açık Yetki | ✅ | Her uç policy'yi açıkça beyan eder: GET → `merchant.read`, mutasyon → `merchant.write`; tekil GET `MerchantScoped` (tenant sınırı), liste + yazma + statü `AdminPlaneOnly`. Token verme statü-kapılı (yalnız Active — 012 davranışı; MerchantProvisioned/kademeli demet bu fazda tetiklenmez, mekanizma Identity'de duruyor). |
| VI. Spec-Driven | ✅ | Tam akış: spec → plan (bu belge) → tasks → implement. |
| Teknoloji kısıtları | ✅ | CPM (sürüm eklenmez — mevcutlar yeter), TL-only (para birimi alanı yok), Aspire AppHost, marker DI gerekmedikçe elle kayıt yok. |
| Test kuralı | ✅ | Yalnız saf domain birim testleri (DB/HTTP yok); handler/endpoint quickstart ile elle doğrulanır. |

**Gate sonucu**: GEÇTİ — ihlal yok, Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/023-merchant-submerchant-model/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md        # Phase 1 çıktısı
├── quickstart.md        # Phase 1 çıktısı
├── contracts/
│   └── merchants-api.md # Phase 1 çıktısı (5 uç + event sözleşmesi)
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/Merchant.Api/
├── Domains/
│   ├── Merchants/                          # YENİ aggregate klasörü (tek AggregateRoot)
│   │   ├── Merchant.cs                     # Aggregate: Create/UpdateDetails/ChangeStatus
│   │   ├── MerchantStatus.cs               # enum: Active, Passive, Suspended
│   │   ├── MerchantType.cs                 # enum: Personal, PrivateCompany, LimitedOrJointStockCompany
│   │   └── Features/
│   │       ├── Commands/
│   │       │   ├── CreateMerchant.cs       # POST   /api/v1/merchants
│   │       │   ├── UpdateMerchant.cs       # PUT    /api/v1/merchants/{merchantId}
│   │       │   └── ChangeMerchantStatus.cs # PUT    /api/v1/merchants/{merchantId}/status
│   │       └── Queries/
│   │           ├── GetMerchant.cs          # GET    /api/v1/merchants/{merchantId}
│   │           └── ListMerchants.cs        # GET    /api/v1/merchants
│   └── SubMerchants/                       # 022 iyzico malzemesi — DOKUNULMAZ (ileriki entegrasyon)
├── Provider/                               # 022 iyzico istemci çekirdeği — DOKUNULMAZ
├── GlobalUsings.cs                         # yeni namespace'ler eklenir
└── Program.cs                              # endpoint map çağrıları eklenir (yayın kayıtları zaten var)

tests/Merchant.Api.Tests/                   # YENİ (022'de silinen desenle: xUnit, csproj CPM'li)
├── Merchant.Api.Tests.csproj
├── GlobalUsings.cs
└── MerchantTests.cs                        # aggregate davranış testleri (saf domain)

PaymentGateway.slnx                         # test projesi eklenir
```

**Structure Decision**: Mevcut Merchant.Api projesi içinde yeni `Domains/Merchants/`
aggregate klasörü (aggregate-klasör kuralı: klasörde tek `: AggregateRoot`);
`SubMerchants/` ve `Provider/` 022 ara-durum malzemesi olarak dokunulmadan kalır
(FR-006 — iyzico entegrasyonu ayrı iş). Test projesi `tests/` altında yeniden doğar
(çözümdeki tek test projesi).

## Complexity Tracking

> İhlal yok — boş.
