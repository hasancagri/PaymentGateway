# Quickstart / Doğrulama: Reference Data BC + SharedKernel

Amaç: feature'ı uçtan uca elle doğrulamak. Sistem her zaman Aspire üzerinden kalkar.

## Ön koşullar
- `dotnet build` yeşil (yeni Reference.Api + SharedKernel dahil, çözüm 0 hata).
- `dotnet run --project src/aspire/AppHost/AppHost.csproj` → Postgres + RabbitMQ + tüm servisler (reference-api dahil) ayakta.
- Testler: `dotnet test tests/Merchant.Api.Tests`, `dotnet test tests/Commission.Api.Tests`, `dotnet test tests/Payment.Api.Tests` — yeşil.

## Senaryo 1 — SharedKernel enum tekleştirme (US2)
1. `grep -rn "enum CardBrand\|enum CardType" src` → yalnız `src/others/SharedKernel/CardTaxonomy/` altında çıkmalı; Payment/Commission'da kopya kalmamalı. **Beklenen**: tek tanım (SC-001).
2. Çözüm derlenir 0 hata; Payment.Api + Commission.Api SharedKernel'e ProjectReference verir.

## Senaryo 2 — Commission grid migration (US2)
1. Migration öncesi: eski komisyon grid'inde `Criteria.CardBrand=VISA(1)`, `CardType=PREPAID(3)` içeren satırlar olsun (seed/elle).
2. Migration çalıştır (startup init veya migration slice).
3. `GET api/v1/bank-commissions?...` → aynı satırlar artık kanonik markaya eşlenmiş döner: VISA→Visa, PREPAID→Prepaid. **Beklenen**: hiçbir satır yanlış markaya kaymaz, PREPAID kaybolmaz (SC-004).
4. Migration'ı ikinci kez çalıştır → veri değişmez (idempotent).

## Senaryo 3 — Reference.Api read + snapshot (US1/US3)
1. `GET api/v1/countries` → TR döner. `GET api/v1/cities?countryCode=TR` → İstanbul/Ankara/... döner.
2. `GET api/v1/mccs?page=1&pageSize=25` → sayfalı MCC. `GET api/v1/banks?page=1&pageSize=25` → sayfalı banka (63 kayıt).
3. `GET api/v1/reference/snapshot` → dört katalog tek yanıtta.

## Senaryo 4 — Merchant onboarding yerel read-model'den doğrular (US1)
1. Aspire ayakta, Merchant read-model event/snapshot ile dolu.
2. `POST merchants` geçerli `countryCode=TR, cityCode=34, mcc=5411` → **başarılı**.
3. Tanımsız `cityCode=99` → **reddedilir** (Türkçe mesaj).
4. `cityCode=34, countryCode=US` (çapraz tutarsız) → **reddedilir** (`BelongsTo`).
5. **Availability testi**: reference-api'yi durdur, Merchant'ı bırak. Aynı onboarding **hâlâ çalışır** (yerel read-model). **Beklenen**: sıcak yolda kesinti yok (SC-003).

## Senaryo 5 — Bank konsolidasyonu (Option B, US1)
1. `grep -rn "class BankCatalog" src` → Merchant + Commission kopyaları **silinmiş**; banka code→ad yalnız Reference.Api'de (SC-002).
2. Commission `POST bank-commissions` (banka kodu ile) → banka adı/varlık Reference-beslemeli read-model'den doğrulanır; grid çalışır (SC-002a).
3. Commission `Bank.SupportedInstallments` hâlâ Commission'da; taksit-banka kısıtı (BulkUpsert) çalışır.

## Senaryo 6 — Bootstrap (US4)
1. Merchant read-model'ini boşalt (ya da taze şema), Merchant'ı yeniden başlat.
2. Açılışta snapshot çekimiyle read-model dolar; ilk `POST merchants` dolu katalogla yanıtlar. **Beklenen**: boş-katalog reddi yaşanmaz (SC-005).

## Senaryo 7 — Event yayılımı (US3)
1. Reference seed'ine yeni bir şehir ekle (seed genişlet), reference-api yeniden başlat → `ReferenceDataUpdated` yayılır.
2. Merchant read-model kısa süre içinde yeni şehri tanır; o şehirle onboarding geçer (SC-006, eventual consistency).

## Notlar
- Migration ve enum tekleştirme en riskli parçalar; Senaryo 1–2 önce doğrulanmalı.
- Contracts detayları: [contracts/reference-api.md](./contracts/reference-api.md). Veri modeli: [data-model.md](./data-model.md).