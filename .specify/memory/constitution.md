<!--
SYNC IMPACT REPORT
==================
Version change: 1.6.0 → 2.0.0
Bump rationale: MAJOR — banka-adaptör pivotu (2026-10-19 kilitlendi). Geriye dönük uyumsuz
  kaldırmalar + domain yeniden tanımı:
  (1) iyzico ödeme kanalı + Merchant BC + Commission BC + Mail.Worker SÖKÜLDÜ → tek Payment BC.
  (2) İlke V'ten merchant-istemci düzlemi (012) ve kademeli statü-yetki (013) KALDIRILDI (merchant
      aggregate'i artık yok); store→gateway S2S platform IdP'sine, müşteri/callback yüzeyi anonim
      (per-session secret token) olarak yeniden tanımlandı; PG artık MCP yüzeyi expose etmez.
  (3) E2E Testing (Playwright) bölümü KALDIRILDI (Admin BFF söküldü; E2E'lenecek UI kalmadı).
  (4) CP.VPOS + Mail.Mcp/Excel.Mcp atıfları KALDIRILDI (ölü).

Modified principles:
  - I. Bounded Context İzolasyonu: örnekler güncellendi (Merchant zengin-aggregate örneği çıktı;
    banka = ayrı BC değil, adapter arkasında dış sistem).
  - II. Zengin Domain Modeli: BAYAT DÜZELTİLDİ — `BaseModel`/`Enumeration` atıfları kaldırıldı;
    conventions.md ile hizalı (tek `AggregateRoot` base; enum aggregate dosyasında; VO record+Create).
  - V. Merkezi Kimlik ve Açık Yetki: makine düzlemi (011) korundu; merchant/kademeli düzlemleri
    kaldırıldı; store→gateway = platform IdP bearer + PG scope; hosted/callback = anonim + secret
    token (fail-closed); PG MCP yüzeyi yok.

Added principles:
  - VI. Kart Verisi ve Sır Minimizasyonu (NON-NEGOTIABLE): PAN PG'de kalıcı olmaz/loglanmaz; 3DS'te
    PAN tarayıcıdan doğrudan bankaya (PG sunucusuna uğramaz, SAQ A-EP hedefi); banka credential'ları
    + callback sırları yalnız sır-store'da; kontrol düzlemi sır döndürmez.
  - VII. Banka Entegrasyonu: Port/Adapter + Anti-Corruption: her banka BC-sahipli adapter arkasında;
    wire tipleri kullanan slice'ta nested (paylaşılan SDK YOK); normalize domain cevabı; routing =
    yetenek eleme + fiyat; banka OTORİTE, PG kopyası öngörü.
  (eski VI Spec-Driven → VIII; eski VII Domain Legibility → IX; içerik korundu.)

Removed sections:
  - E2E Testing (Playwright) — Admin BFF söküldüğü için kaldırıldı.

Deferred TODOs:
  - TODO(SSO_CONSOLIDATION): Identity.Server şirket-içi tek SSO'ya evrilecek; üç IdP (PG Identity /
    EC Identity / AgentPlatform IdP) konsolidasyonu ayrı tasarım oturumu (pivot memory kilidi).
  - TODO(STORE_CONTRACT): store→gateway kontrat transport (HTTP/gRPC) + webhook imza şeması detayı
    yeni Payment BC spec'inin clarify adımında netleşir.

Templates/commands: Bağımlı şablonlar anayasayı çalışma anında okur; senkron gerektiren tutarsızlık
  yok. CLAUDE.md + docs/conventions.md pivotla bayatladı → ayrı güncellenecek (teardown PR'ında).
-->

# PaymentGateway Constitution

PaymentGateway, bir e-ticaret store'unun müşterilerinden yalnız TL ödeme kabul etmesini sağlayan,
banka-adaptörlü bir ödeme ağ geçididir. Ödeme yönü **pay-by-link**'tir: kart bilgisi sohbete/agent'a
girmez; tek kullanımlık TTL'li bir link üzerinden PG'nin hosted ödeme sayfasında toplanır, sonuç
store'a webhook'la bildirilir. Bankalara erişim, banka başına bir adapter arkasından sağlanır. Bu
anayasa projenin pazarlık edilemez ilkelerini tanımlar; CLAUDE.md *nasıl uygulanır* rehberidir, bu
belge *ne pazarlık edilemez* sorusunu yanıtlar. Çakışma olursa anayasa kazanır.

## Core Principles

### I. Bounded Context İzolasyonu (NON-NEGOTIABLE)

Her mikroservis tek bir Bounded Context'tir ve sınırı fiziksel olarak serttir.

- Her context'in kendi veritabanı, kendi şeması ve kendi domain modeli olmak ZORUNDADIR;
  paylaşılan ortak bir domain modeli OLMAMALIDIR.
- Bir servis, başka bir servisin veritabanına, tablosuna veya aggregate'ine doğrudan
  erişemez. Bu KESİN olarak yasaktır.
- Context'ler arası iletişim yalnızca (a) `Shared` içindeki bilinçli olarak paylaşılan
  integration event kontratları ve (b) anlık tutarlılık zorunlu olduğunda sanksiyonlu
  senkron kanal (gRPC/HTTP) üzerinden yapılır.
- Aynı kavram farklı context'te farklı modeldir; model bir context'ten diğerine sızdırılMAZ.
- **Banka bir BC değildir**: dış bir sistemdir ve yalnız BC-sahipli bir adapter arkasından
  erişilir (bkz. İlke VII). Banka wire tipleri domain/slice sınırını GEÇMEZ.

Gerekçe: İzolasyon, servislerin bağımsız evrilmesini ve dağıtılmasını mümkün kılar;
paylaşılan veritabanı veya model, mikroservis mimarisini gizli bir monolite çevirir.

### II. Zengin Domain Modeli ve Invariant'lar

İş kuralları domain modelinin içinde yaşar, dışında değil.

- Anemik (davranışsız) aggregate YASAKTIR. Aggregate'ler private setter + statik `Create`
  fabrikası + davranış metotlarıyla yazılır.
- Aggregate root tutarlılık sınırıdır ve tek ortak `AggregateRoot` base'inden türer (Id +
  denetim alanları); dış dünya aggregate'i yalnız kök üzerinden değiştirir. Entity base ALMAZ.
- Invariant ihlalleri handler'da değil, aggregate metodunda yakalanır. Koleksiyonlar private
  tutulur ve yalnızca okunur olarak expose edilir; mutasyon yalnız aggregate metotlarından geçer.
- Kimliği olmayan kavramlar Value Object (record/class + private ctor + statik `Create`) ile
  modellenir. Enum'lar ayrı base/dosya almaz — ait oldukları aggregate dosyasında tanımlanır.

Gerekçe: Kuralı tek bir yerde (aggregate) toplamak, tutarlılığı garanti eder ve iş mantığının
handler'lara dağılıp çoğalmasını önler.

### III. Vertical Slice + CQRS

Kod teknik katmana göre değil, domain feature'ına göre organize edilir.

- Feature'lar `Domains/<Aggregate>/Features/{Commands,Queries}` altında yer alır. Bir feature
  = bir static class'tır; command/query `record`'unu, `Response`'unu, `Handler`'ını ve
  endpoint-extension'ını içine gömer.
- Yazma ve okuma ayrı slice'lardır. Durum değiştiren işlemler `Commands/` altında olur ve
  handler'ı `[Transactional]` ile işaretlenir; yalnız veri döndürenler `Queries/` altındadır.
- Repository deseni KULLANILMAZ. Handler'lar kalıcılık için doğrudan Marten `IDocumentSession`,
  başka bir slice'ı çağırmak için `IMessageBus` alır.
- Endpoint'ler Minimal API'dir, `*EndpointExtension` metotlarıyla map'lenir ve handler'ı
  `IMessageBus.InvokeAsync` ile çağırır.
- Kullanıcı tetiklemeyen süreç-güdümlü handler'lar `Domains/` DIŞINDADIR (`Saga/` = dış
  orchestrator katılımı, `Process/` = BC'nin kendi dayanıklı süreci — ör. mutabakat).

Gerekçe: Bir feature'ın tüm parçalarını tek dosyada tutmak, değişimi yerelleştirir ve
katmanlar-arası dağılmayı ortadan kaldırır.

### IV. Result Pattern (exception'suz beklenen hata akışı)

Beklenen hatalar (bulunamadı, doğrulama, iş kuralı ihlali) exception ile DEĞİL, Result
nesnesiyle taşınır.

- Handler'lar, aggregate metotları ve endpoint'ler `Common.Results` altındaki tiplerden
  (`FeatureObjectResultModel<T>`, `FeatureResultModel`, `ResultDomain` ...) birini döner.
  Bu tipler statik fabrika metotlarıyla üretilir, `new` ile değil.
- Hata bilgisi `MessageItem` ile taşınır; `Code` serbest metin DEĞİL, bir resource sabitidir.
- Çok-durumlu domain sonucu (ör. ödeme sonucu) outcome-enum'la taşınır; retry-able "başarısız"
  değeri teknik `Error`'a eşlenmez.
- Exception yalnızca gerçekten beklenmeyen durumlar içindir; onları `GlobalExceptionHandler`
  yakalar.

Gerekçe: Beklenen hataları tipli sonuçlarla taşımak akışı öngörülebilir kılar ve kontrol
akışının exception'lara bağlanmasını önler.

### V. Merkezi Kimlik ve Açık Yetki

Kimlik doğrulama merkezîdir ve hiçbir korunması gereken uç açıkta bırakılMAZ.

- Makine (M2M) düzlemi KARARLIDIR (011): kimlik, OpenIddict tabanlı merkezi Identity servisi
  (`Identity.Server`, sabit issuer `https://localhost:5101`) tarafından verilir; servisler bu
  otoriteye göre (JWKS üzerinden, paylaşılan DB olmadan) kimlik doğrular. client_credentials +
  scope-tabanlı policy; kural GET → `<bc>.read`, durum değiştiren → `<bc>.write`; access token'da
  scope claim'i JSON dizisidir.
- Durum değiştiren veya hassas veri döndüren her endpoint ve mesaj handler'ı, erişim için gereken
  yetkiyi AÇIKÇA beyan etmek ZORUNDADIR; "varsayılan açık" uç bırakılMAZ.
- **Store → gateway (S2S)**: store'un ödeme başlatma çağrısı (pay-by-link) platform IdP'si
  (AgentPlatform) tarafından verilen token ile doğrulanır; PG, bu token'ı PG-özel scope ile
  yetkilendiren bir relying party'dir. İç makine düzlemi (Identity.Server) ile ayrı otoritedir.
- **Müşteri ve banka-callback yüzeyleri anonimdir** ama fail-closed korunur: hosted ödeme sayfası
  ve banka/store callback uçları, tahmin edilemez **per-session secret token** ile kapılıdır
  (kimlik yok, sır URL/path'te taşınır, eşleşmezse reddedilir).
- **PG bir MCP yüzeyi expose etMEZ.** Agent/kontrol-düzlemi yüzeyi PG'de yaşamaz (banka operasyon
  MCP'si ayrı simülatör repo'sundadır). Ödeme akışı hiçbir koşulda MCP'ye girmez.

Gerekçe: Ödeme sistemi için yetki, sonradan eklenen değil baştan tasarlanan bir kısıttır;
her yüzey (makine, store-S2S, anonim-müşteri) kendi otoritesiyle ve açıkça korunur.

### VI. Kart Verisi ve Sır Minimizasyonu (NON-NEGOTIABLE)

PG, taşımak zorunda olmadığı hiçbir kart verisini veya sırrı tutmaz.

- **PAN kalıcı olmaz**: tam kart numarası PG veritabanına YAZILMAZ, log/trace'e DÜŞMEZ; işlem
  defterinde yalnız maskeli gösterim + banka referans/token tutulur. Kart saklama (vault)
  bilinçli olarak kapsam DIŞIDIR.
- **3DS'te PAN PG sunucusuna uğramaz**: kart verisi tarayıcıdan doğrudan bankaya iletilir; PG'nin
  hesapladığı hiçbir hash PAN içermez. Hedef PCI kademesi SAQ A-EP'tir.
- **Sırlar yalnız sır-store'da**: banka credential'ları (SecretKey vb.) ve callback imza sırları
  yapılandırma/secret-store'da (user-secrets/env) tutulur; non-secret config'ten (Options POCO)
  ayrıdır. Bir sır ASLA log/trace/tool-dönüşü/query DTO'sunda görünmez.
- **Kontrol düzlemi sır döndürmez**: REST/tool dönüşleri credential İÇERMEZ. Makine-handoff gerekirse
  yalnız S2S kayıt (bootstrap-key) + HMAC-imzalı dayanıklı callback ile yapılır; sır insana render
  EDİLMEZ.

Gerekçe: Ödeme ağ geçidinin en büyük yükümlülüğü kart verisi ve sırdır; tutmadığın veri sızdıramazsın.
PCI yüzeyini dar tutmak mimari bir zorunluluktur, sonradan düşünülecek bir detay değil.

### VII. Banka Entegrasyonu: Port/Adapter + Anti-Corruption

Her bankaya erişim, BC-sahipli bir port ardında, banka başına bir adapter ile sağlanır.

- Ortak bir **port** (NonSecure/3DSecure sale + ileride iptal/iade/durum/BIN) tanımlanır; her banka
  bu portu kendi protokolüyle (ör. `;;` ayraçlı InterVpos, XML+SHA512) gerçekleyen bir **adapter**
  ile bağlanır. Paylaşılan bir banka SDK'sı ÜRETİLMEZ (YAGNI + anti-corruption).
- Banka wire request/response tipleri, kullanan slice'ın İÇİNDE nested kalır; domain/BC sınırını
  GEÇMEZ. Adapter, wire cevabını normalize bir domain cevabına (başarı/red + red taksonomisi)
  çevirir; VO↔wire eşlemesi anti-corruption sınırıdır.
- **Yönlendirme (routing)** iki aşamalıdır: önce yetenek eleme (kart tipi/taksit/3D modeli uygun mu),
  sonra fiyat/on-us sıralaması. Yönlendirme verisi (PosAccount yetenek + fiyat matrisi) PG'de bir
  ÖNGÖRÜ kopyasıdır; banka OTORİTEDİR, bayatlık mutabakatta yakalanır.
- `timeout` bir "başarısız" DEĞİLDİR: belirsiz sonuç ayrı sınıflanır (çift çekim tuzağı), durum
  sorgusu/mutabakatla netleşene dek terminal kabul edilmez.

Gerekçe: Çok-bankalı bir ağ geçidinin değeri, banka farklılıklarını tek bir kararlı iç modelin
ardına gizlemektir; wire tipini paylaşmak bu sınırı deler ve bir bankanın değişimini hepsine bulaştırır.

### VIII. Spec-Driven Development

Önemsiz olmayan her değişiklik spec-kit akışından geçer.

- Akış: `/speckit-constitution` → `/speckit-specify` → (`/speckit-clarify`) → `/speckit-plan`
  → `/speckit-tasks` → `/speckit-implement`. Doğrudan koda atlamadan önce en azından spec üretilir.
- Anayasa her şeyin üstündedir. Bir spec, plan veya kod anayasayla çelişemez; çelişirse ya
  koda uydurulur ya da anayasa gerekçeli bir amendment ile güncellenir.
- Artefakt seti feature büyüklüğüne göre ölçeklenir: _trivial_ değişiklik spec'siz olabilir;
  _küçük_ feature yalnız `spec.md` + `tasks.md` üretir; _tam_ feature tam akıştan geçer.
  Şüphede kalınırsa bir üst kademe seçilir.
- Domain-TDD: saf domain (aggregate davranışı, saga `On*`, VO) test-first yazılır; test task'ı
  implementasyondan önce gelir. Handler/endpoint/UI/altyapı bu kuralın dışıdır (test-sonra/canlı).

Gerekçe: Spec-driven akış, kararların kod yazılmadan önce yazılı ve gözden geçirilmiş olmasını
sağlar; anayasa bu akışa tutarlılık zemini verir.

### IX. Domain Süreci Legibility (EventStorming-belgelenmiş süreç)

Her Bounded Context'in domain süreci **okunabilir olmak zorundadır**: BC'nin iş akışı
(hangi adım, hangi sırayla, hangi komut/olay/policy) **EventStorming altitude'unda**,
ubiquitous dille (teknoloji değil, iş/ürün anlatısı olarak) belgelenir ve **domain süreci
değiştikçe** güncel tutulur.

- Kapsam **domain sürecidir** — iş adımlarının sırası ve doğurdukları olaylar; class-by-class
  çağrı dökümü DEĞİL. Teknoloji yalnız koda-atlama köprüsü olarak kenarda kalır.
- Güncelleme tetiği **dardır**: yeni/silinen command-event-policy ya da adım sırası değişimi.
  Mekanik refactor/rename süreci değiştirmez → belgeyi tetiklemez (o drifti guard yakalar).
- Domain süreci belgesi **olmayan ya da bayat** (koddaki güncel süreçle çelişen) bir BC kabul
  edilemez. Bir feature domain sürecini değiştiriyorsa belge **aynı PR'da** güncellenir.
- Bu bir **süreç disiplinidir** (İlke VIII gibi), yapı invariant'ı değil. Anayasa yalnız *ne +
  neden* der; belgenin **dosya adı, formatı ve senkron-guard'ı** "nasıl" olarak
  `conventions.md`/`CLAUDE.md`'de tanımlanır.

Gerekçe: DDD'nin çekirdeği domain legibility'dir. Koda uzun süre uzak kalan sahibin bile süreci
geri yükleyebilmesi, domain'in teknolojinin ardında kaybolmaması gerekir.

## Teknoloji ve Alan Kısıtları

- **Platform:** .NET 10; her projede `Nullable` ve `ImplicitUsings` açıktır.
- **Orkestrasyon:** .NET Aspire. Sistem her zaman AppHost üzerinden ayağa kalkar; servisler
  bağımlılıklarını (Postgres, RabbitMQ, diğer servisler) Aspire service discovery + connection
  string enjeksiyonu ile bulur. Tek servisi izole çalıştırmak desteklenen bir yol değildir.
- **Kalıcılık (karma):** Marten (Postgres). Ödeme/işlem defteri (ör. `PaymentTransaction`)
  **event stream** olarak tutulur (Wolverine `[AggregateHandler]`); hesap/config/BIN gibi referans
  veri **düz doküman**dır. EF Core yalnız izole altyapı servislerinde (ör. Identity) kullanılır.
- **Mesajlaşma:** Wolverine — süreç-içi command/query bus'ı ve RabbitMQ üstünden fanout
  integration mesajlaşması. Deterministik mesaj `[Transactional]` outbox ile gider (publish yalnız
  DB commit'te). Handler keşfi ad-son-eki bazlıdır (`*Handler`/`*Consumer`); bulunamıyorsa
  `opts.Discovery.IncludeType(...)` ZORUNLUDUR.
- **Paket yönetimi:** Central Package Management. Sürümler yalnız `Directory.Packages.props`'ta
  tanımlanır; `.csproj` dosyaları `PackageReference`'ı sürümsüz listeler.
- **DI:** `ITransientDependency`/`IScopedDependency`/`ISingletonDependency` marker arayüzleri +
  Scrutor otomatik kaydı. Elle `services.Add...` yerine marker tercih edilir.
- **Config:** Options pattern (tip'li POCO). `IConfiguration`'dan doğrudan okuma YASAKTIR;
  her section `Options/` POCO'suna bağlanır (`BindConfiguration`+`Validate`), tüketici düz `T` enjekte eder.
- **Alan kısıtı — yalnız TL:** Sistem yalnızca TL işlemi destekler; yabancı para birimi
  DESTEKLENMEZ.

## Geliştirme Akışı

- **Dil:** Kod yorumları, XML dokümanları, commit mesajları ve kullanıcıya dönük doğrulama
  mesajları Türkçe yazılır; mevcut konvansiyon korunur.
- **Using'ler:** Her projenin tek bir `GlobalUsings.cs`'i vardır; paylaşılan namespace'ler
  oraya eklenir.
- **Test:** Testler saf domain birim testleridir (host/entegrasyon harness'ı yok); banka/dış
  HTTP çağrıları test edilmez (geliştirmede stub adapter / sonra simülatör ile doğrulanır).
  Davranışlı aggregate'ler ve saf domain servisleri (ör. yönlendirme hesabı) test için önceliklidir.
- **Runtime rehberi:** CLAUDE.md günlük *nasıl uygulanır* rehberidir ve anayasaya tabidir;
  ikisi çakışırsa anayasa kazanır.

## Governance

- Bu anayasa diğer tüm pratiklerin üstündedir. Bir uygulama kararı anayasayla çelişemez.
- **Amendment prosedürü:** Değişiklik, bu dosyada gerekçesiyle birlikte önerilir, sürüm numarası
  aşağıdaki kurala göre güncellenir ve Sync Impact Report'a işlenir.
- **Sürümleme (semantic):** MAJOR = geriye dönük uyumsuz ilke kaldırma/yeniden tanımlama;
  MINOR = yeni ilke/bölüm ekleme veya rehberliğin belirgin genişlemesi; PATCH = açıklama,
  ifade veya yazım düzeltmesi.
- **Uyum denetimi:** Her spec, plan ve kod incelemesi anayasaya uygunluğu doğrulamak
  ZORUNDADIR. Ek karmaşıklık ancak gerekçelendirilerek eklenir (YAGNI).
- Ertelenen kararlar (TODO) Sync Impact Report'ta takip edilir ve karar netleştiğinde
  amendment ile kapatılır.

**Version**: 2.0.0 | **Ratified**: 2026-07-29 | **Last Amended**: 2026-10-20