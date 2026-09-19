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

## Domain süreci belgesi — FLOW.md (İLKE VII uygulaması)

Anayasa "her BC'nin domain süreci belgelenir" der; **nasıl**'ı budur:

- **Dosya:** her BC kökünde tek `FLOW.md` (ör. `src/services/Payment.Api/FLOW.md`). Kod-yakını → az bayatlar.
- **IDE görünürlüğü:** bu repo'da servis klasörü DÜZ (`.csproj` doğrudan BC kökünde, ECommerce'teki gibi
  bir alt `<Bc>.Api/` katmanı YOK) → FLOW.md zaten Solution Explorer'da görünür, linked-file hilesi
  gerekmez. Non-.NET/altyapı worker'lar (ör. `Mail.Worker`) için de aynı — dosya proje kökünde durur.
- **Altitude:** domain-önce, ubiquitous dille — "hangi iş adımı, hangi sırayla, hangi olayı doğurur".
  Teknoloji/class **dökümü değil**; class adı yalnız satır sonunda `(Aggregate.Metot → Event)` **kenar-
  anchor** (koda atlama). Satır numarası YOK (bayatlar).
- **İçerik:** (1) BC ne yapar tek cümle, (2) sıralı **Süreç** adımları, (3) **Domain kuralları**
  (süreci yöneten değişmezler), (4) **Sınır** (BC'nin dokunmadığı). ~1 ekran.
- **Güncelleme tetiği (dar):** yalnız domain süreci değişince (yeni/silinen command-event-policy, adım
  sırası). Mekanik rename/refactor tetiklemez. Feature süreci değiştiriyorsa FLOW.md **aynı PR'da** güncellenir.
- **Guard:** `scripts/check-flow-links.sh` — FLOW.md'deki kenar-anchor tip adlarının kod tabanında hâlâ
  VAR olduğunu doğrular (rename/silme driftini yakalar). Sıra driftini yakalamaz — o review + tetik disiplini.

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
  Features/
    Commands/<Name>.cs            # yazma slice'ları
    Queries/<Name>.cs             # okuma slice'ları
    Agents/                       # agent'a açık slice'lar (klasör ÇOĞUL; MCP expose eder)
      Commands/<Name>.cs          # yazma agent slice'ı
      Queries/<Name>.cs           # okuma agent slice'ı
```

- **MCP tool sarmalayıcısı slice'ıyla AYNI dosyada yaşar** (dosya sonunda, `[McpServerToolType]`),
  ayrı `<Aggregate>McpTools.cs` YOK. Gerekçe: "bir feature = bir dosya" ilkesi transport katmanına da
  uzanır; ayrıca tool kaydı `WithToolsFromAssembly()` ile assembly-geneli taranır, dosya konumu bağımsız.
  Aggregate'in TÜM MCP yüzeyini görmek gerekirse `grep -rl McpServerToolType Domains/<Aggregate>/`.
- **`Agents/Commands` + `Agents/Queries` alt klasörü, cache-invalidation kararını KLASÖRDEN okunur
  yapar** (`Commands/*` → `[InvalidatesCache]` adayı, `Queries/*` → `[Cached]` adayı) — dosya açmadan,
  yalnız konuma bakarak. `Agents/Commands|Queries`, üst-seviye `Features/Commands|Queries`'ten AYRI
  (Bilinçli tekrar: agent slice o klasörlerle kod paylaşmaz, yalnız isim benzerliği).
- **Sınıf adı ÇIPLAK feature adıdır, suffix YOK** (`ForAgent`/`Command`/`Query` eklenmez) — konum zaten
  bunu söylüyor (`Agents/Commands/SubmitRegistration.cs` → `SubmitRegistration`). Suffix, klasör ayrımı
  yokken (eski flat `Agents/<Name>ForAgent.cs`) telafi ediyordu; ayrım varken tekrar gereksiz.
- **Bir feature = bir static class**: `record` command/query + `Response` + `Handler` (düz sınıf,
  `Handle` metodu) + endpoint-extension. Command mı query mi ayır, doğru klasöre koy.
- **Yapı hazır, doldurmak ihtiyaç güdümlü (JIT).** İskelet nereye ne konacağını gösterir; ama her
  aggregate metodu için endpoint ÜRETME zorunluluğu YOK. Endpoint = gerçek tüketici çağırınca açılır.
- **`Domains/` = kullanıcı isteği; süreç güdümlü handler `Domains/` DIŞINDA.** `Features/{Commands,
  Queries,Agents}` = "biri (müşteri/admin, endpoint/MCP/agent) bunu İSTEDİ" niyet yüzeyi. Kullanıcının
  tetiklemediği, süreç-güdümlü handler'lar feature slice DEĞİL → `Domains/` dışına, iki klasöre:
  `Saga/` = başka BC'nin sağa-orchestrator'ının broker komutuyla bu BC'yi süren katılım handler'ları;
  `Process/` = bu BC'nin KENDİ dayanıklı süreci (watchdog/reconcile, `ScheduleAsync` tick'i). Okuma
  testi: "kullanıcı mı tetikledi, süreç mi?" → kullanıcı=Domains, dış-saga=Saga, iç-süreç=Process.
  Aggregate davranışı her iki yoldan da çağrılsa Domains'te kalır; ikisinin paylaştığı saf helper de
  Domains'te (aggregate değil, ortak altyapı — ör. bir BC'nin fanout'tan beslenen salt-okur referans
  belgesi). Taşınan yalnız süreç-glue'su (handler + mesajı).
- **Süreç güdümlü dosya/sınıf adı = kaynağın adı + `Consumers`.** Kaynak = event/komutu yayınlayan BC
  ya da worker. `Saga/`'da kaynak = sağayı yöneten orchestrator; kökte (Saga/Process dışı, doğrudan
  servis kökünde) kaynak = yayıncı BC (ör. `MerchantApiConsumers.cs`). **Bir dosya = bir kaynak** —
  aynı serviste birden fazla BC'den event geliyorsa kaynak başına ayrı dosya; dosya adından "bu nereden
  geliyor" cevaplanır, içerik açmaya gerek kalmaz. `Process/` bu kuralın dışı (kaynak başka BC/worker
  değil, BC'nin KENDİ dayanıklı süreci).
- **Wolverine keşfi ad-son-eki bazlıdır: `*Handler`/`*Consumer` ile bitmeyen handler sınıfı taramayla
  BULUNMAZ** (çoğul `*Consumers` ve `Process/` sınıfları dahil) → `Program.cs`'e
  `opts.Discovery.IncludeType(typeof(X))` ZORUNLU. Unutulursa mesaj sessizce yutulur — hata da
  dead-letter da yok. Rename'de bu kayıt da güncellenir; yeni handler açarken ilk kontrol bu satır.
- **Tek çağıranı sanksiyonlu S2S araç (gRPC/A2A vb.) olan Features slice'ı Domains dışına çıkar, o
  aracın SINIFI İÇİNE gömülür.** "Kullanıcı mı tetikledi" testi burada da geçerli — REST/MCP/agent hiç
  çağırmıyorsa (yalnız S2S servisi tüketiyorsa) o slice sahte bir "niyet yüzeyi" değildir, indirekt S2S
  glue'dur. Ayrı `Features/Commands|Queries/<Name>.cs` + `IMessageBus.InvokeAsync` hop'u yerine mantık
  doğrudan S2S servis class'ına yazılır. **Bilinçli tekrar kabul edilir** — aynı sorgu/komutun agent/MCP
  muadili varsa paylaşılmaz, S2S tarafı kendi kopyasını taşır. **İstisna:** aynı slice'ı hem sanksiyonlu
  S2S hem başka somut kullanıcı/agent yolu da çağırıyorsa (paylaşılan yazım yolu) Domains'te KALIR.
  (Bugün PG'de bu paternin canlı örneği yok — gRPC/A2A internal S2S henüz yok; kural ileriye dönük.)
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
  domain-service/seeder/endpoint-extension/enum aynı BC'de aggregate kökünde durabilir (MCP tool YOK —
  o slice dosyasında yaşar, bkz. VSA dosya yapısı).
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
  messaging/HTTP. MCP tool YALNIZ `Features/Agents/Commands|Queries/<X>` slice'ını çağırır (ince sarmalayıcı).

## Bilinçli tekrar (tek gerekçe)

Bazı yerlerde kod BİLEREK tekrarlanır — kırılgan bağımlılık/paylaşım yerine BC izolasyonu + süreç
netliği tercih edilir. Aşağıdakiler bu kurala dayanır, ayrı ayrı gerekçelendirilmez:

- Aggregate'te davranış private helper'a parçalanmaz (davranış inline; guard helper'ı istisna).
- Agent slice `Features/Commands|Queries`'e gitmez (kendi handler'ını taşır, `IMessageBus` ile bile değil).
- Dış-sistem wire tipleri paylaşılan SDK'ya çıkarılmaz, kullanan slice'ın içine nested (anti-corruption sınır).
- Generic hata kodu servisler arası tekrarlanır.
