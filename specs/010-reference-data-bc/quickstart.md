# Quickstart / Doğrulama: Reference Data BC + SharedKernel

Amaç: feature'ı uçtan uca elle doğrulamak. Sistem her zaman Aspire üzerinden kalkar.

> **Mimari not (kullanıcı pivotu):** Reference.Api **event-only** — HİÇ HTTP/GET yüzeyi yok.
> Katalog kaynak-of-truth Reference.Api'de; tüketiciler (Merchant, Commission) yerel read-model'i
> yalnız `ReferenceDataUpdated` fanout event'iyle doldurur. Açılışta `ReferenceStartupPublisher`
> katalog tam-setini yayar; taze tüketici durable queue (`merchant.reference-sync`,
> `commission.reference-sync`) + idempotent upsert ile dolar. Snapshot/GET uçları YOKTUR.

## Ön koşullar
- `dotnet build` yeşil (yeni Reference.Api + SharedKernel dahil, çözüm 0 hata).
- `dotnet run --project src/aspire/AppHost/AppHost.csproj` → Postgres + RabbitMQ + tüm servisler (reference-api dahil) ayakta.
- Testler yeşil: `dotnet test tests/{Reference,Merchant,Commission,Payment}.Api.Tests`.

## Senaryo 1 — SharedKernel enum tekleştirme (US2)
1. `grep -rn "enum CardBrand\|enum CardType" src` → yalnız `src/others/SharedKernel/CardTaxonomy/` altında çıkmalı; Payment/Commission'da kopya kalmamalı. **Beklenen**: tek tanım (SC-001).
2. Çözüm derlenir 0 hata; Payment.Api + Commission.Api SharedKernel'e ProjectReference verir.

## Senaryo 2 — Commission grid migration (US2)
1. Migration öncesi: eski komisyon grid'inde `Criteria.CardBrand=VISA(1)`, `CardType=PREPAID(3)` içeren satırlar olsun (seed/elle).
2. Commission.Api açılır → `RemapCardTaxonomyMigration` (BackgroundService) eski int'leri kanonik sete remap eder (tek geçiş, tam sözlük).
3. Migration sonrası grid satırları kanonik markaya eşlenir: VISA→Visa(0), PREPAID→Prepaid(2). **Beklenen**: hiçbir satır yanlış markaya kaymaz, PREPAID kaybolmaz (SC-004).
4. Commission.Api'yi yeniden başlat → migration yalnız `TaxonomyVersion < 1` kayıtları işler, veri değişmez (idempotent).
5. Geriye uyum: eski `"VISA"`/`"PREPAID"` string'leri `Criteria.FromCodes` ile hâlâ parse eder (`ignoreCase`).

## Senaryo 3 — Reference.Api event yayını (US1/US3) — RabbitMQ
1. Aspire dashboard → RabbitMQ. `reference.data-updated` fanout exchange var.
2. reference-api açılışta `ReferenceStartupPublisher` katalog tam-setini (Country/City/MCC/Bank) `ReferenceDataUpdated` ile yayar.
3. Durable queue'lar (`merchant.reference-sync`, `commission.reference-sync`) exchange'e bağlı ve mesaj tüketmiş. **Beklenen**: tüketici read-model'leri dolu (GET uçları yok, DB'den doğrula).

## Senaryo 4 — Merchant onboarding yerel read-model'den doğrular (US1)
1. Aspire ayakta, Merchant read-model event ile dolu (`ReferenceCountry/City/Mcc/Bank`, id=Code).
2. `POST merchants` geçerli `countryCode=TR, cityCode=34, mcc=5411` → **başarılı**.
3. Tanımsız `cityCode=99` → **reddedilir** (Türkçe mesaj).
4. `cityCode=34, countryCode=US` (çapraz tutarsız) → **reddedilir** (`ReferenceKey`/`BelongsTo` doğrulaması handler'da `LoadAsync`).
5. **Availability testi**: reference-api'yi durdur, Merchant'ı bırak. Aynı onboarding **hâlâ çalışır** (yerel read-model). **Beklenen**: sıcak yolda kesinti yok (SC-003).

## Senaryo 5 — Bank konsolidasyonu (Option B, US1)
1. `grep -rn "class BankCatalog" src` → Merchant + Commission kopyaları **silinmiş**; banka code→ad yalnız Reference read-model'de (SC-002).
2. Commission `POST bank-commissions` (banka kodu ile) → banka adı/varlık Reference-beslemeli read-model'den `LoadAsync` ile doğrulanır; grid çalışır (SC-002a).
3. Commission `Bank.SupportedInstallments` hâlâ Commission'da; taksit-banka kısıtı (BulkUpsert) çalışır.

## Senaryo 6 — Bootstrap / taze tüketici (US4)
1. Merchant read-model'ini boşalt (ya da taze şema), Merchant'ı yeniden başlat.
2. reference-api ayaktayken durable queue kalıcı olduğundan tüketici yeniden bağlanır; gerekirse reference-api restart → `ReferenceStartupPublisher` tam-seti tekrar yayar.
3. Read-model dolar; ilk `POST merchants` dolu katalogla yanıtlar. **Beklenen**: boş-katalog reddi yaşanmaz (SC-005).

## Senaryo 7 — Event yayılımı (US3)
1. Reference seed'ine yeni bir şehir ekle (seed genişlet), reference-api yeniden başlat → `ReferenceDataUpdated` yayılır (diff veya tam-set).
2. Merchant read-model kısa süre içinde yeni şehri tanır; o şehirle onboarding geçer (SC-006, eventual consistency).

## Notlar
- Migration ve enum tekleştirme en riskli parçalar; Senaryo 1–2 önce doğrulanmalı.
- Reference.Api'de GET/HTTP yok → tüketici read-model'leri DB (Marten şeması) veya davranış (onboarding) üzerinden doğrulanır.
- Contracts detayları: [contracts/reference-api.md](./contracts/reference-api.md). Veri modeli: [data-model.md](./data-model.md).