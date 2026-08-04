# Phase 0 Research: Reference Data BC + SharedKernel

Kararlar mevcut proje desenlerine (agent taraması + reference-architecture.md) ve clarify oturumuna dayanır.

## R1 — Servis bootstrap (Reference.Api iskeleti)

**Decision**: Commission.Api/Merchant.Api Program.cs desenini birebir kopyala.
- Marten: `opts.DatabaseSchemaName = SchemaConstants.ReferenceSchemaName ("referenceManagement")`, `opts.Connection(referenceDb)`, `UseNewtonsoftForSerialization(...)`, `Schema.For<Country/City/Mcc/Bank>()`, `.IntegrateWithWolverine().ApplyAllDatabaseChangesOnStartup()`, `.InitializeWith(new ReferenceSeeder())`.
- Wolverine: `Durability.Mode = Solo` (dev), `UseRabbitMq(conn).AutoProvision()`, `DeclareExchange(Reference exchange, Fanout)`, `PublishMessage<ReferenceDataUpdated>().ToRabbitExchange(...)`, `UseDurableLocalQueues()`, `Discovery.IncludeAssembly(...)`.
- API versioning + `AddGlobalExceptionHandler()` + `AddAllDependencies()` (Scrutor).
- AppHost: `var referenceDb = postgres.AddDatabase("referenceDb")`; `AddProject<Projects.Reference_Api>("reference-api").WithReference(referenceDb).WithReference(rabbit).WaitFor(...)`.
- csproj refs: Common, Shared, ServiceDefaults (SharedKernel gerekmez — Bank yalnız code→ad).

**Rationale**: Anayasa "her servis AppHost üzerinden, aynı desen"; sıfır yeni altyapı kararı.

**Alternatives**: Sıfırdan minimal host — reddedildi (desen tutarsızlığı, anayasa İlke ihlali riski).

## R2 — Integration event: ilk gerçek TÜKETİCİ

**Decision**: Bu feature projedeki **ilk event consumer**'ı kurar (bugüne dek yalnız Payment publish ediyor, tüketici yok). Reference.Api publisher; Merchant + Commission consumer.
- Publisher (Reference.Api): fanout exchange `reference.data-updated` (RabbitMqConstants'a eklenir), `ReferenceDataUpdated` yayar.
- Consumer (Merchant/Commission Program.cs): `rabbit.DeclareExchange(...)` + **durable queue** bind (`rabbit.BindExchange(RefExchange).ToQueue("merchant.reference-sync")` deseni), Wolverine `Handle(ReferenceDataUpdated)` metodu `EventHandlers`'ta assembly taramasıyla keşfedilir.
- **publish-then-save** + at-least-once + idempotent upsert (Q4).

**Rationale**: reference-architecture 007 ingestion deseni (publish-then-save, idempotent, durable queue, DLQ) birebir uygulanır.

**Alternatives**: Transient queue (restart'ta kayıp) — reddedildi (Q4). Senkron çağrı — reddedildi (İlke I).

## R3 — Tüketici read-model + sıcak yol okuma

**Decision**: İki katman:
1. **Kalıcı read-model** = Marten dokümanı (tüketicinin kendi şemasında), event handler idempotent upsert eder. Kaynak-of-truth kopyası, restart'ta kalıcı.
2. **Sıcak yol okuma** = mevcut `ILookup`/`ICountryLookup`/`ICityLookup`/`IMccLookup`/`IBankCodeLookup` arabirimleri KORUNUR; implementasyonları artık gömülü listeyi değil bu read-model'i okur. Singleton yerine küçük katalog için scoped Marten sorgusu ya da bellek-cache'li singleton (declarative `[Cached]` opsiyonel) — implementasyon detayı, arabirim sabit.

**Rationale**: FR-010 "tüketici iş kodu değişmez" — arabirim sabitliği bunu garanti eder. StorefrontView precedent: read-model satırı, aggregate değil.

**Alternatives**: Tüketici koduna doğrudan Marten sorgusu serpmek — reddedildi (arabirim soyutlaması korunmalı, iş kodu değişmemeli).

## R4 — Bootstrap (taze tüketici)

**Decision**: Reference.Api `GET /reference/snapshot` ucu tüm kataloğu tek yanıtta döner (küçük veri, sayfasız snapshot). Tüketici açılışta (hosted service / Wolverine startup) yerel read-model boşsa bir kez çeker, sonra event'lerle güncel kalır. Boot-zamanı senkron çağrı; sıcak yol değil.

**Rationale**: reference-architecture'da snapshot üretici-tarafında; taze tüketici için tek-seferlik boot pull en basit ve deterministik. Read-model dolduktan sonra bir daha çağrılmaz.

**Alternatives**: Tam event yeniden-yayın (üretici tüm kataloğu event olarak basar) — daha çok hareketli parça, ertelendi. Event replay (kalıcı log) — RabbitMQ fanout transient, event-sourcing gerektirir, aşırı.

## R5 — Enum migration (Commission grid) — SERİLEŞTİRME DOĞRULANDI (int)

**Serileştirme (DOĞRULANDI)**: Üç serviste de Marten Newtonsoft'u **StringEnumConverter'sız** kullanır (Payment Program.cs:15-20 teyit) → enum'lar **int** serileşir. BinCard seed JSON da int kod tutar (`cardType:1`). O hâlde Commission grid dokümanlarındaki `Criteria.CardBrand`/`CardType` **int** kalıcıdır → **int remap gerekir** (string rename değil).

**Decision — CardBrand**: Kanonik = Payment seti (Unknown=-1, Visa=0, MasterCard=1, Troy=2, Amex=3, Discover=4, Unionpay=5, JCB=6). Commission eski (VISA=1, MASTERCARD=2, TROY=3, AMEX=4) → kanonik remap:
```
VISA(1)→Visa(0), MASTERCARD(2)→MasterCard(1), TROY(3)→Troy(2), AMEX(4)→Amex(3)
```
Payment tarafı değişmez (kanonik = Payment değerleri).

**Decision — CardType (YENİ SORUN → superset)**: Payment `Debit=0, Credit=1` (2 değer, **Prepaid yok**); Commission `CREDIT=1, DEBIT=2, PREPAID=3` (3 değer). Kanonik Payment'a birebir eşitlenirse Commission'ın **PREPAID satırları kaybolur** (FR-015 orphan). Çözüm: **kanonik CardType = Payment seti + Prepaid ile genişlet** (superset), Payment değerlerini koru:
```
kanonik: Debit=0, Credit=1, Prepaid=2   (+ istenirse Unknown=-1)
Commission remap: CREDIT(1)→Credit(1) [int aynı], DEBIT(2)→Debit(0) [değişir], PREPAID(3)→Prepaid(2) [değişir]
```
Payment'ta Prepaid satırı yok → genişletme Payment verisini etkilemez, veri dönüşümü YOK. Commission remap eder.
**ONAYLANDI (2026-08-03)**: kanonik CardType = `Unknown=-1, Debit=0, Credit=1, Prepaid=2` (Payment + Prepaid superset). Kullanıcı onayladı; veri kaybı yok.

**Migration mekanizması**: idempotent tek-seferlik veri dönüşümü (Marten startup init ya da elle migration slice). Her grid dokümanının (`BankCommission`, `MerchantCommission`) `Criteria` int'lerini eski→kanonik tablosuyla çevir. İdempotent: bir versiyon/işaret alanı ya da "değer zaten kanonik aralıkta ve eski aralıkla çakışmıyor" kontrolü. **Dikkat — çakışma tuzağı**: CardBrand MASTERCARD(2)→MasterCard(1) ama TROY eski=3, yeni Troy=2... remap SIRALI yapılırsa üst üste biner (2→1, sonra 3→2 zaten dönüşeni tekrar çevirir). Migration **tek geçişte, eski→yeni tam eşleme sözlüğüyle** (in-place değil, kaynak int'e göre) uygulanmalı; kısmi/sıralı güncelleme yasak.

**API isim etkisi**: Commission `Criteria.FromCodes` string→enum parse eder ve `GetCriteriaOptions` `Enum.GetNames` döner. Kanonik isimler (`Visa` vs `VISA`, `Credit` vs `CREDIT`) değişir → Commission create API'sinin kabul ettiği string'ler ve dropdown seçenekleri değişir. Parse **case-insensitive** yapılmalı (eski `VISA` string'i de kabul) ya da Admin/çağıran yeni isimlere güncellenmeli. Bu API-görünür değişiklik not edilmeli.

**Rationale**: Kanonik=Payment; Payment verisi dokunulmaz. Grid ayrıca banka **kodu** (string, stabil) taşır — o etkilenmez. Prepaid superset veri kaybını önler.

**Alternatives**: String rename (serileştirme string olsaydı) — geçersiz (int doğrulandı). Prepaid'i düşürmek — veri kaybı, reddedildi. Commission seti kanonik — reddedildi (Payment seçildi).

## R6 — Embedded JSON seed

**Decision**: BinCard (008) desenini kopyala: `Domains/<Entity>/Data/<entity>.json` embedded resource; `ReferenceSeeder : IInitialData`, `AnyAsync()` ile idempotent, `GetManifestResourceStream` + `JsonConvert.DeserializeObject`. Seed sonrası (veya seed'de değişiklik varsa) `ReferenceDataUpdated` yayınla ki tüketiciler ilk dolumu event üzerinden de alabilsin (snapshot ile birlikte iki yol).

**Rationale**: 008'de kanıtlanmış, CP.VPOS bağımlılığı yok.

**Alternatives**: DB migration seed / dış dosya — reddedildi (embedded JSON proje standardı).

## R7 — Sayfalama (MCC/Bank read API)

**Decision**: 009 ListBinCards desenini kopyala: `List<Entity>Query(filtreler..., int Page, int PageSize)`, clamp (Default 25, Max 100, page<1→1), `session.Query<>().Where(...).OrderBy(...).ToPagedListAsync(page,pageSize,ct)`, response `{Items, TotalCount, Page, PageSize, PageCount}`. Country/City küçük → sayfasız da olabilir; City ülke filtreli.

**Rationale**: 009'da kanıtlanmış pager; MCC ~1000+ ve Bank listesi sayfalama ister.

## R8 — SharedKernel proje bağımlılıkları

**Decision**: `SharedKernel.csproj` minimum bağımlılık (yalnız BCL); yalnız enum'lar. Payment.Api + Commission.Api ProjectReference verir. CPM içinde. Commission `Criteria` (SharedKernel enum kullanır) + Payment `BinCard`/`BinCardMapping` bu enum'lara referans verir.

**Rationale**: Shared kernel dar ve stabil olmalı (co-owned sözleşme); bağımlılık şişmesi bulaşmayı artırır.

**Alternatives**: Enum'ları `Shared`'a koymak — reddedildi (Shared = integration event kontratları; domain sözcüğü ayrı sınır, daha temiz — Q1 kararı yeni proje).