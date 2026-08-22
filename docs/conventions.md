# Mimari Konvansiyonlar (taşınabilir katman)

Bu dosya proje-bağımsızdır: DDD/VSA mimarisi + kod disiplini. Bu proje mimariyi
ECommerceWithAgentFramework'ten devraldı — konvansiyonlar iki repo'da aynıdır. Proje-özel bilgi
(komutlar, servis listesi, feature'lar) `CLAUDE.md`'dedir. **İLKE N** = projenin
`.specify/memory/constitution.md` ilkesi.

## Spec-Driven Development (spec-kit)

Önemsiz olmayan her feature spec-kit akışıyla (`.claude/skills/speckit-*`): `constitution → specify
→ clarify(ops) → plan → tasks → implement`. Koda atlamadan önce en az spec (gerekirse plan) üretilir.

- **Anayasa (`.specify/memory/constitution.md`) her şeyin üstünde.** Bu konvansiyonlar "nasıl uygulanır",
  anayasa "ne pazarlık edilemez" — çakışırsa **anayasa kazanır**. İlkeye İLKE N ile atıf yapılır;
  ilkenin kendisini tekrar etme, anayasadan oku.
- **Artefakt ölçekleme:** _trivial_ = spec-kit'siz; _küçük_ (tek aggregate, yeni tablo/kontrat/event
  yok) = yalnız `spec.md`+`tasks.md`; _tam_ (yeni aggregate/tablo/event/kontrat) = tam akış. Şüphede üst kademe.
- **Domain-TDD (İLKE VI):** saf domain (aggregate davranışı, saga `On*`, VO) test-first; test task'ı
  implementasyondan önce. Handler/endpoint/UI/altyapı bu kuralın dışı (test-sonra/canlı doğrulama).

## Mimari kurallar (anayasa-atıflı)

Her rol için: anayasa İLKE = "ne"; buradaki satır = "nasıl uygulanır" (koda özgü, anayasada olmayan).

- **BC izolasyonu (İLKE I).** Her servis = 1 BC = kendi Postgres DB'si + Marten şeması; DB paylaşımı YOK.
  Başka BC'nin aggregate/tablo/DbContext'ine erişim yasak; context'ler arası tek kanal: integration
  event + MCP (yalnız agent) + sanksiyonlu senkron kanal (gRPC/A2A/HTTP).
- **Aynı kavram farklı BC'de farklı model (İLKE I).** Bir kavram bir BC'de zengin aggregate,
  ötekinde sade entity, üçüncüde read-model satırı olabilir — birinin modelini ötekine sızdırma.
- **Zengin aggregate (İLKE II).** Ortak `AggregateRoot`'tan türer (Id + denetim alanları); anemik yasak.
  Entity base ALMAZ. VO = `record`/class + private ctor + statik `Create`.
- **Invariant aggregate içinde (İLKE II).** Koleksiyon private, okuma `IReadOnlyList`, mutasyon yalnız
  aggregate metodundan. **Yeni kural → önce aggregate metoduna bak, handler'a değil.**
- **VSA + CQRS (İLKE III).** Kod teknik katmana değil domain feature'ına göre (yapı aşağıda). Command
  (`[Transactional]`, `IDocumentSession` yazar) / Query (yalnız okur) ayrı slice. **Repository YOK** —
  handler doğrudan `IDocumentSession`, slice-arası çağrı `IMessageBus`. Endpoint = Minimal API.
- **Result pattern (İLKE IV).** Beklenen hata = Result (exception değil); detay Kod standartlarında.
- **Scope yetki (İLKE V).** Servisler talep ettikleri scope'larla korunur; her endpoint policy'yi
  AÇIKÇA beyan eder (GET → `<bc>.read`, mutasyon → `<bc>.write`). Kurulum detayı proje CLAUDE.md'de.

### VSA dosya yapısı

```
Domains/<Aggregate>/
  <Aggregate>.cs                  # zengin aggregate root (private setter, factory + davranış)
  <Aggregate>EndpointExtension.cs # feature endpoint'lerini gruplar + map'ler
  <Aggregate>McpTools.cs          # bu aggregate için MCP tool sarmalayıcıları (aggregate kökünde)
  Features/
    Commands/<Name>.cs            # yazma slice'ları
    Queries/<Name>.cs             # okuma slice'ları
    Agents/<Name>ForAgent.cs      # agent'a açık slice (klasör ÇOĞUL; MCP expose eder)
```

- **Bir feature = bir static class**: `record` command/query + `Response` + `Handler` (düz sınıf,
  `Handle` metodu) + endpoint-extension. Command mı query mi ayır, doğru klasöre koy.
- **Ayrı teknik-katman klasörü YOK.** MCP tool dahil tüm feature/süreç `Domains/<Aggregate>/` altında;
  `McpTools/` gibi teknik klasör açma. Assembly taraması konumdan bağımsızdır.
- **Yapı hazır, doldurmak ihtiyaç güdümlü (JIT).** İskelet nereye ne konacağını gösterir; ama her
  aggregate metodu için endpoint ÜRETME zorunluluğu YOK. Endpoint = gerçek tüketici çağırınca açılır.
- API sürümleme URL-segment (`v1`); doküman Scalar ile kök.

## Kod standartları

- **Result — handler.** `FeatureObjectResultModel<T>`/`FeatureResultModel`/`FeatureListResultModel`/
  `FeaturePagedResultModel` döner (`Common.Results`, `new` YOK — statik fabrika `Ok`/`Error`/`NotFound`).
  Hata `MessageItem.Code` = resource sabiti (serbest metin değil).
- **Result — aggregate.** Davranış/fabrika metotları `ResultDomain`/`ResultDomain<T>` döner (void mutator
  dahil; fabrikalar `Ok(data)` sarar). Çağıran: `var r = agg.Method(...); if(!r.IsSuccess) return <err>(r.Messages);`.
  **Muaf:** saf getter/lookup.
- **Result — outcome-enum.** Çok-durumlu domain sonucu `Ok(outcome)`'la taşınır; "başarısız" enum değeri
  `Error`'a eşlenmez (retry-able Failed teknik hata değildir).
- **Hata kodu sahipliği.** Her servis kendi kodlarına sahip: `<Service>/Constants/<Service>ResourceConstants.cs`.
- **Aggregate klasör.** `Domains/<X>/` hemen altı tek `: AggregateRoot`; iç içe aggregate yok. İstisna:
  domain-service/seeder/MCP-tool/endpoint-extension/enum aynı BC'de aggregate kökünde durabilir.
- **VO tek dosya.** Aggregate'e ait standalone VO `<Aggregate>/ValueObjects/` altına konur (aggregate kökünde değil).
- **Enum aggregate dosyasında** (`OrderStatus` → `Order.cs`); ayrı dosya/`Enumeration` base yok.
- **Aggregate davranışını private helper'a parçalama** — davranış mantığı inline (bilinçli tekrar).
  İSTİSNA: guard/invariant kontrolü helper'a çıkarılabilir (aynı guard'ın 3. kopyası tutarsızlık üretir). **VO muaf.**
- **Aggregate metodu yalnız handler'dan çağrılır** — başka aggregate metodundan (factory dahil) değil. **VO muaf.**
- **Aggregate public metodu:** `/// <summary>` metodun ne yaptığını yazar.

## Konvansiyonlar

- **Madde ≤300 karakter.** Tüm repo dokümanları (spec/tasks/CLAUDE.md/constitution). Aşan maddeyi böl
  veya ayrıntıyı ilgili yere taşı; tasks.md ne yapılacağını listeler, nasılını değil.
- **GlobalUsings:** her projede tek `GlobalUsings.cs`; paylaşılan namespace oraya, dosyaya `using` serpme.
- **`Domains/` yalnız domain:** teknik sabit (resource kodu) `<Service>/Constants/`'e, `Domains/`'e değil.
- **DI = Scrutor otomatik:** `ITransientDependency`/`IScopedDependency`/`ISingletonDependency` marker'ı;
  `AddAllDependencies()` kaydeder. `Program.cs`'te elle kayıt yapma.
- **Agent tipleri Singleton** — framework başlangıçta yakalar; per-user davranış = token'ı çağrı anında enjekte.
- **Config = Options pattern (tip'li).** `IConfiguration`'dan DOĞRUDAN okuma YASAK (`config["A:B"]`,
  `GetValue<T>`, `Get<T>()` dahil). Her section → `Options/` POCO'su; tüketici düz `T` enjekte eder (`IOptions<T>` değil).
- **Options bağlama:** `AddOptions<T>().BindConfiguration(nameof(T)).ValidateDataAnnotations().ValidateOnStart()`.
  **İstisna:** service-discovery + dinamik-key lookup (statik section değil).

## Servisler-arası desenler

- **Integration event.** Kontrat paylaşılan sözleşme kitaplığında; Wolverine→RabbitMQ **fanout**.
  Yayıncı exchange deklare eder, **binding'i TÜKETİCİ kurar**. Deterministik mesaj `[Transactional]`
  outbox ile — publish yalnız DB commit'te gider. Additive alan default'lu ekle (eski tüketici kırılmaz).
- **TUZAK — Wolverine handler keşfi.** Integration-event tüketicisi `public static class` + `public
  static async Task Handle(<Event> msg, ...)` olmalı; sınıf adı **"Handler" ile TEKİL** bitmeli.
- **"Handlers" (çoğul) Wolverine 6.4'te SESSİZCE keşfedilmez** — "No known handler ... discarded",
  dead-letter YOK, mesaj kaybolur. Emin değilsen `opts.Discovery.IncludeType(typeof(...))`; canlı doğrula
  (log'da "Successfully processed" var, "No known handler" yok).
- **Sanksiyonlu senkron kanal (İLKE I).** Anlık-tutarlılık akışı için gRPC/A2A/HTTP; çağıran karşının
  API'sine erişir (DB'sine değil). Sunucu ince sarmalayıcı (iş mantığı yok, `IMessageBus`'a devreder).
- **MCP yalnız agent tüketir.** Agent olmayan kod (servis/UI) imperatif `CallToolAsync` süremez →
  messaging/HTTP. MCP tool YALNIZ `Features/Agents/<X>ForAgent` slice'ını çağırır (ince sarmalayıcı).

## Bilinçli tekrar (tek gerekçe)

Bazı yerlerde kod BİLEREK tekrarlanır — kırılgan bağımlılık/paylaşım yerine BC izolasyonu + süreç
netliği tercih edilir. Aşağıdakiler bu kurala dayanır, ayrı ayrı gerekçelendirilmez:

- Aggregate'te davranış private helper'a parçalanmaz (davranış inline; guard helper'ı istisna).
- Agent slice `Features/Commands|Queries`'e gitmez (kendi handler'ını taşır, `IMessageBus` ile bile değil).
- Dış-sistem wire tipleri paylaşılan SDK'ya çıkarılmaz, kullanan slice'ın içine nested (anti-corruption sınır).
- Generic hata kodu servisler arası tekrarlanır.
