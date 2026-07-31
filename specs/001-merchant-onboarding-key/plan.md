# Implementation Plan: Merchant Onboarding + API Key + Admin

**Branch**: `001-merchant-onboarding-key` | **Date**: 2026-07-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-merchant-onboarding-key/spec.md`

## Kapsam notu (bu dilim)

Spec üç kullanıcı hikayesi (merchant+key, komisyon, seed admin) içeriyor. Bu **plan dilimi**
yalnız iki yeni Marten + Wolverine bounded context'i kapsar:

- **Merchant.Api** — merchant registry (US1'in registry ayağı; key üretimi hariç)
- **Commission.Api** — banka + merchant komisyonu (US2)

**Bu dilim dışında (sonraki dilim):** Identity.Server genişletme (`umk_` key, provision,
merchant_id claim, seed admin), Admin BFF (Razor Pages), senkron provisioning orkestrasyonu,
scope enforcement, Marten conjoined multitenancy. Bunlar Obsidian `DropShop/Yapılacaklar.md`
+ tasarım dokümanında izleniyor.

## Summary

İki bağımsız BC. **Merchant.Api**: `Merchant` aggregate (isim, iletişim, adres, MCC, webhook,
durum) — global registry, source of truth. **Commission.Api**: iki aggregate — `BankCommission`
(gateway maliyeti; banka × kart-kombinasyonu × taksit → oran) ve `MerchantCommission` (gateway
geliri; merchant × kart-kombinasyonu × taksit → oran). Invariant `merchantRate > bankRate`
**in-process** zorlanır (Commission tek serviste iki aggregate tutar; cross-call yok). Merchant
tenant izolasyonu bu dilimde düz `MerchantId` alan filtresiyle (Marten multitenancy ertelendi).

Teknik yaklaşım: her BC `Payment.Api` şablonuyla (Marten document store + Wolverine bus + vertical
slice + Result pattern). Legacy `otherProjects` yalnız domain referansı — bire bir taşınmaz.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten 9.5, WolverineFx 6.x (+ Marten/RabbitMQ/Http), Scrutor,
Asp.Versioning, .NET Aspire (AppHost). `Common` (Result, domain base, DI marker) + `Shared`.

**Storage**: PostgreSQL — `merchantDb` (Marten), `commissionDb` (Marten). Her BC kendi şeması/DB'si.
Multitenancy YOK bu dilimde (düz `MerchantId` filtresi).

**Testing**: Saf domain birim testleri (xUnit). Öncelik: `Merchant.Create` doğrulamaları
(e-posta, MCC 4-hane, webhook, zorunlu alan) + `MerchantCommission` invariant (`> bankRate`,
taksit-taksit eşleşme). HTTP çağrıları test edilmez. Test projesi bu dilimde ilk kez eklenir.

**Target Platform**: Linux/container; Aspire ile lokal orkestrasyon (Postgres + RabbitMQ)

**Project Type**: Mikroservis backend (2 yeni BC) — web

**Performance Goals**: Etkileşimli admin akışı, düşük hacim. p95 < 500ms uç başına; SC-001
insan hızı sınırlı.

**Constraints**: `merchantRate > bankRate` %100 zorlanır (SC-003); merchant komisyon listesinde
başka merchant sızıntısı 0 (SC-004, düz filtre); yalnız TL (oran yüzde, para birimi modellenmez).

**Scale/Scope**: Düşük hacim. 2 yeni servis + AppHost + Common (2 sabit) + test projesi.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar denetlenir.*

| İlke | Durum | Not |
|---|---|---|
| I. Bounded Context İzolasyonu | ✅ PASS | Merchant.Api + Commission.Api ayrı DB. **Cross-call yok**: Commission `MerchantId`'yi `Guid` olarak alır, Merchant.Api'ye sormaz. Banka/merchant oranı aynı serviste → invariant in-process, dağıtık invariant yok. |
| II. Zengin Domain Modeli | ✅ PASS | `Merchant`, `BankCommission`, `MerchantCommission` private setter + statik `Create` + davranış + invariant metotta. `Criteria` value object; `CardBrand`/`CardType`/`TransactionRegion` `Enumeration`. Koleksiyon yoksa da invariant aggregate'te. |
| III. Vertical Slice + CQRS | ✅ PASS | `Domains/<Aggregate>/Features/{Commands,Queries}`; handler `[Transactional]` + `IDocumentSession`, repository yok; endpoint `*EndpointExtension` + `IMessageBus`. |
| IV. Result Pattern | ✅ PASS | `FeatureObjectResultModel<T>` / `ResultDomain`; `MessageItem.Code` resource sabiti. |
| V. Merkezi Kimlik + Açık Yetki | ⚠️ ERTELENDİ | Uçlar bu dilimde **korumasız** (CLAUDE.md "yetkilendirme yok, Identity BC ile gelecek" ile tutarlı). Scope enforcement (`merchants.manage`/`commissions.manage`) Identity dilimiyle gelir. İzlenen bilinçli erteleme; anayasa V (TODO AUTHZ_MODEL) hâlâ açık. |
| VI. Spec-Driven | ✅ PASS | Bu akış. |
| Alan: yalnız TL | ✅ PASS | Komisyon = yüzde oran. |
| Kalıcılık: Marten | ✅ PASS | İki BC de Marten. |

**Gate sonucu**: GEÇTİ. Tek erteleme (İlke V, yetkisiz uçlar) CLAUDE.md'nin açık bilinçli
ertelemesiyle örtüşüyor; Identity dilimi kapatacak. Gerekçesiz ihlal yok → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/001-merchant-onboarding-key/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — kararlar (bu dilim)
├── data-model.md        # Phase 1 — aggregate/value object/enum modeli
├── quickstart.md        # Phase 1 — uçtan uca doğrulama senaryosu
├── contracts/           # Phase 1 — servis uç kontratları
│   ├── merchant-api.md
│   └── commission-api.md
└── tasks.md             # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/
├── services/
│   ├── Merchant.Api/                     # YENİ — Marten + Wolverine BC (global registry)
│   │   ├── Domains/Merchants/
│   │   │   ├── Merchant.cs               # aggregate
│   │   │   ├── MerchantStatus.cs         # Enumeration: Active/Passive/Suspended
│   │   │   ├── MerchantEndpointExtension.cs
│   │   │   └── Features/
│   │   │       ├── Commands/CreateMerchant.cs
│   │   │       └── Queries/{GetMerchant,GetAllMerchants}.cs
│   │   ├── Dependencies/DependencyExtensions.cs
│   │   ├── GlobalUsings.cs  Program.cs  Merchant.Api.csproj
│   ├── Commission.Api/                   # YENİ — Marten + Wolverine BC (2 aggregate)
│   │   ├── Domains/
│   │   │   ├── BankCommissions/
│   │   │   │   ├── BankCommission.cs      # gateway maliyeti (global)
│   │   │   │   ├── BankCommissionEndpointExtension.cs
│   │   │   │   └── Features/{Commands/CreateBankCommission, Queries/GetBankCommissions}.cs
│   │   │   ├── MerchantCommissions/
│   │   │   │   ├── MerchantCommission.cs  # gateway geliri; invariant > bankRate
│   │   │   │   ├── MerchantCommissionEndpointExtension.cs
│   │   │   │   └── Features/
│   │   │   │       ├── Commands/{CreateMerchantCommission,UpdateMerchantCommission}.cs
│   │   │   │       └── Queries/GetMerchantCommissions.cs
│   │   │   └── Shared/                    # Criteria + CardBrand/CardType/TransactionRegion Enumeration
│   │   │       ├── Criteria.cs
│   │   │       ├── CardBrand.cs  CardType.cs  TransactionRegion.cs
│   │   ├── Dependencies/DependencyExtensions.cs
│   │   ├── GlobalUsings.cs  Program.cs  Commission.Api.csproj
│   ├── Payment.Api/   CP.VPOS/   gateway/   # mevcut (dokunulmaz)
├── others/  (Common: + AuthorizationScopes sabitleri SONRAKİ dilim; bu dilim dokunmaz)
└── aspire/
    └── AppHost/AppHost.cs                # + merchantDb, commissionDb; merchant-api, commission-api

tests/
└── Commission.Domain.Tests/             # YENİ — saf domain birim testleri
    ├── MerchantTests.cs                  # (Merchant.Api'ye referans)
    └── MerchantCommissionTests.cs        # invariant testleri
```

**Structure Decision**: Mevcut `src/services` (Marten+Wolverine BC), `src/aspire` (AppHost)
düzeni korunur. İki yeni BC `src/services` altına `Payment.Api` şablonuyla eklenir (aynı Program.cs
Marten+Wolverine wiring, `Dependencies`, vertical slice). Commission.Api iki aggregate'i tek serviste
tutar; ortak `Criteria` + enum'lar `Domains/Shared` altında. Test projesi yeni `tests/` kökünde.
Legacy `otherProjects` yalnız domain referansı; kod kopyalanmaz.

## Complexity Tracking

> Gerekçe gerektiren anayasa ihlali yok. (İlke V ertelemesi CLAUDE.md'nin mevcut bilinçli
> ertelemesiyle örtüşüyor; yeni bir sapma değil.)