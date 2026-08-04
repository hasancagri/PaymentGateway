# Implementation Plan: Reference Data BC + Shared Card Taxonomy Kernel

**Branch**: `010-reference-data-bc` | **Date**: 2026-08-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/010-reference-data-bc/spec.md`

## Summary

İki yapı, tek feature: (1) yeni **Reference.Api** bounded context'i statik katalog verisinin (Country/City/MCC/Bank code→ad) kaynak-of-truth'u olur — kendi Postgres şeması, embedded-JSON seed, sayfalı salt-okuma API, `ReferenceDataUpdated` fanout event. (2) yeni **SharedKernel** projesi tekrar eden card taksonomi enum'larını (`CardBrand`, `CardType`) tek kaynağa indirir; Payment+Commission yerel kopyayı siler, referans verir, Commission grid'inin kalıcı verisi kanonik (Payment) int setine remap edilir. Tüketiciler (Merchant, Commission) katalog verisini Reference.Api olaylarıyla beslenen **yerel read model** üzerinden okur (StorefrontView deseni); doğrulama sıcak yolu daima yerel, senkron dış bağımlılık yok (eventual consistency). Bootstrap: taze tüketici açılışta bir kez snapshot çeker; dayanıklılık idempotent upsert + durable queue + DLQ ile (ECommerce 007 ingestion deseni).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-proc bus + RabbitMQ fanout), .NET Aspire, Marten.Newtonsoft/Newtonsoft.Json, Scrutor (convention DI), Asp.Versioning.Http. CPM (`Directory.Packages.props`).

**Storage**: PostgreSQL. Yeni `referenceDb` + `referenceManagement` şeması (Reference.Api kaynak-of-truth). Tüketici read model'leri kendi mevcut şemalarında (merchantManagement, commissionManagement) ayrı Marten dokümanı.

**Testing**: Saf domain birim testleri (xUnit) — anayasa gereği host/entegrasyon harness yok. Öncelik: katalog aggregate Create invariant'ları, enum migration eşleme fonksiyonu (idempotent + eksiksiz), read-model upsert idempotency, çapraz tutarlılık (city→country).

**Target Platform**: Linux server, Aspire AppHost ile ayağa kalkar (Postgres + RabbitMQ container).

**Project Type**: Mikroservis bounded context (web service) + paylaşılan sözcük kütüphanesi (SharedKernel class library).

**Performance Goals**: Katalog küçük (MCC ~1000+, Bank/Country/City orta). Doğrulama sıcak yolu yerel (bellek-destekli read model) — ağ yok. Katalog güncellemesi eventual (saniye-altı zorunluluğu yok).

**Constraints**: Doğrulama sıcak yolunda **senkron dış servis bağımlılığı YOK**. Anlık tutarlılık gerekmez → gRPC yok. Yetkilendirme kapsam dışı (anayasa TODO(AUTHZ_MODEL)). CP.VPOS tipleri sınırı geçmez (bu feature CP.VPOS'a dokunmaz).

**Scale/Scope**: 1 yeni BC + 1 yeni shared proje; 3 mevcut servis (Merchant/Commission/Payment) + AppHost + Shared değişir. Admin UI kapsam dışı (sonraki spec).

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

| İlke | Durum | Not |
|------|-------|-----|
| I. Bounded Context İzolasyonu | ✅ PASS | Reference.Api kendi db/şema/model. Tüketici Reference DB'sine ERİŞMEZ; yalnız integration event + yerel read model. Sıcak yolda senkron çağrı yok. SharedKernel = bilinçli paylaşılan **sözleşme** (Shared.IntegrationEvents kategorisi), paylaşılan *domain modeli* değil — bkz Complexity Tracking. |
| II. Zengin Domain Modeli | ✅ PASS (dikkat) | Reference aggregate'leri (Country/City/Mcc/Bank) statik `Create` fabrikası + invariant (kod formatı, ad boş değil, city→country tutarlılık) taşır → anemik değil. Tüketici read-model **satırı** bilinçli olarak davranışsız izdüşümdür (StorefrontView precedent), aggregate değil. |
| III. Vertical Slice + CQRS | ✅ PASS | `Domains/<Entity>/Features/{Commands,Queries}`. v1 salt-okuma → Queries + seed ağırlıklı. Tüketici read-model güncelleme = integration event handler (`EventHandlers`). |
| IV. Result Pattern | ✅ PASS | Aggregate `Create`/migration → `ResultDomain`; handler → `FeatureObjectResultModel<T>`. |
| V. Merkezi Kimlik & Açık Yetki | ⚠️ DEFERRED | Endpoint'ler proje geneli gibi korumasız (anayasa TODO(AUTHZ_MODEL)). Bu feature yetki eklemez. |
| VI. Spec-Driven | ✅ PASS | specify→clarify→plan akışı izlendi. |

**Teknoloji/alan kısıtları:** .NET 10 + Aspire + Marten + Wolverine + CPM + Scrutor DI marker + Türkçe mesaj/yorum — hepsi mevcut desene uyar. CP.VPOS dokunulmaz.

**Gate sonucu: PASS** (V ertelemesi anayasa onaylı; SharedKernel gerekçesi Complexity Tracking'te).

## Project Structure

### Documentation (this feature)

```text
specs/010-reference-data-bc/
├── plan.md              # Bu dosya
├── spec.md              # Özellik
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (read API + event + snapshot sözleşmeleri)
└── checklists/requirements.md
```

### Source Code (repository root)

```text
src/
├── others/
│   ├── SharedKernel/                      # YENİ proje (class library)
│   │   ├── SharedKernel.csproj            #   CPM içinde; bağımlılık minimum
│   │   ├── GlobalUsings.cs
│   │   └── CardTaxonomy/
│   │       ├── CardBrand.cs               #   kanonik (Payment int seti)
│   │       └── CardType.cs                #   kanonik
│   └── Shared/
│       ├── IntegrationEvents.cs           # + ReferenceDataUpdated event kontratı
│       ├── RabbitMqConstants.cs           # + Reference.* exchange adı
│       └── Utils/Constants/SchemaConstants.cs   # + ReferenceSchemaName
├── services/
│   ├── Reference.Api/                     # YENİ bounded context
│   │   ├── Reference.Api.csproj           #   refs: Common, Shared, ServiceDefaults, (SharedKernel gerekmez — Bank code→ad enum içermez)
│   │   ├── Program.cs                      #   Marten(referenceManagement)+Wolverine+RabbitMQ publish + seed
│   │   ├── GlobalUsings.cs
│   │   ├── Dependencies/DependencyExtensions.cs
│   │   └── Domains/
│   │       ├── Countries/ {Country.cs, Features/Queries, CountryEndpointExtension.cs, Data/countries.json}
│   │       ├── Cities/    {City.cs, Features/Queries, CityEndpointExtension.cs, Data/cities.json}
│   │       ├── Mccs/      {Mcc.cs, Features/Queries (sayfalı), MccEndpointExtension.cs, Data/mccs.json}
│   │       ├── Banks/     {Bank.cs (code→ad), Features/Queries (sayfalı), BankEndpointExtension.cs, Data/banks.json}
│   │       ├── Snapshot/  {Features/Queries/GetReferenceSnapshot.cs, SnapshotEndpointExtension.cs}
│   │       └── Seeding/   {ReferenceSeeder.cs (IInitialData, idempotent), event yayını}
│   ├── Merchant.Api/                      # DEĞİŞİR (tüketici)
│   │   └── Domains/.../ReferenceReadModel/ {ReferenceCatalog read-model + EventHandlers + Bootstrap + ILookup impl}
│   │       # SİL: Domains/Merchants/Lookups/{LookupData,LookupRefs} gömülü veri; SettlementAccounts/Lookups/BankCatalog kopyası
│   ├── Commission.Api/                    # DEĞİŞİR (tüketici + enum + migration)
│   │   └── Domains/.../ReferenceReadModel/ {banka code→ad read-model + EventHandlers + Bootstrap}
│   │       # SİL: Domains/Banks/BankCatalog.cs (code→ad); Domains/SharedKernel/{CardBrand,CardType}
│   │       # MIGRATION: komisyon grid dokümanlarında CardBrand/CardType int remap
│   └── Payment.Api/                       # DEĞİŞİR (yalnız enum)
│       # SİL: Domains/BinCards/{CardBrand,CardType} → SharedKernel'e referans; değer aynı (kanonik=Payment) → veri migrationı YOK
└── aspire/AppHost/AppHost.cs             # + referenceDb + reference-api projesi
```

**Structure Decision**: Mevcut mikroservis + `src/others` paylaşılan katman deseni korunur. Yeni BC `src/services/Reference.Api` (Commission/Merchant iskeletiyle birebir). Yeni paylaşılan sözcük `src/others/SharedKernel` (Common/Shared ile aynı seviye). Tüketici read-model'leri her BC'nin kendi `Domains/` altında yerel dokümandır — BC izolasyonu gereği kopya (cross-db sorgu yasak).

## Complexity Tracking

> Anayasa Check'te gerekçe gerektiren iki karar:

| Karar | Neden Gerekli | Reddedilen Basit Alternatif |
|-------|---------------|------------------------------|
| **Yeni `SharedKernel` projesi** (paylaşılan enum) | "Aynı enum iki serviste olmasın" direktifi; card şeması (Visa/Master/...) evrensel, stabil, co-owned sözcük. Anayasa İlke I paylaşılan *domain modelini* yasaklar ama `Shared.IntegrationEvents` gibi **bilinçli paylaşılan sözleşmeleri** açıkça izinli tutar — enum bu kategoride (değer-tipi kontrat, davranış/aggregate değil). | (a) Enum'u kopyalı bırakmak = direktif ihlali + ıraksama borcu. (b) Reference.Api'ye runtime veri yapmak = tip güvenliği + grid anahtarı + invariant kırılır (spec'te reddedildi). |
| **Tüketici yerel read-model** (referans verinin kopyası) | BC izolasyonu (İlke I) tüketicinin Reference DB'sine sorgu atmasını yasaklar; doğrulama sıcak yolu senkron dış bağımlılık taşıyamaz. Yerel kopya + event besleme tek uyumlu yol. | Senkron çağrı (Reference.Api'ye her doğrulamada) = availability coupling + İlke I ihlali; spec'te reddedildi. |