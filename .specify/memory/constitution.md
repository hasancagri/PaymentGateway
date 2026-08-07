<!--
SYNC IMPACT REPORT
==================
Version change: 1.0.0 → 1.1.0
Bump rationale: MINOR — İlke V rehberliği belirgin genişledi: kimlik motoru somutlandı
  (Duende IdentityServer → OpenIddict tabanlı merkezi Identity servisi, 011) ve makine
  düzlemi yetki modeli KARARLI hâle geldi (scope-tabanlı). İlke kaldırılmadı/uyumsuz
  yeniden tanımlanmadı → MAJOR değil.

Modified principles:
  - V. Merkezi Kimlik ve Açık Yetki: motor adı güncellendi (OpenIddict); TODO(AUTHZ_MODEL)
    daraltıldı — makine (M2M) düzlemi scope-tabanlı olarak sabitlendi; insan/rol düzlemi ve
    merchant-istemci düzlemi (G2/G3) açık kaldı.
Added sections: (yok)
Removed sections: (yok)

Deferred TODOs:
  - TODO(AUTHZ_MODEL) [DARALTILDI, 011]: Makine düzlemi KARARLI — client_credentials +
    scope-tabanlı policy (kural: GET → <bc>.read, mutasyon → <bc>.write; endpoint policy'yi
    açıkça beyan eder). Açık kalanlar: (a) insan/rol düzlemi (G3 — login + RBAC),
    (b) merchant-istemci düzlemi (G2 — client_id=merchantId, client_secret=MerchantKey,
    status-gated scope). Bu iki düzlem kendi spec döngüsünde amendment ile kapatılacak.

Templates/commands: Bağımlı şablonlar (plan/spec/tasks) anayasayı çalışma anında okur;
  bu güncellemeyle senkronizasyon gerektiren bir tutarsızlık yok.
-->

# PaymentGateway Constitution

PaymentGateway (ürün adı: DropShop), merchant'ların e-ticaret sitelerinden yalnız TL
ödeme kabul etmesini sağlayan, mikroservis mimarili bir ödeme ağ geçididir. Bu anayasa
projenin pazarlık edilemez ilkelerini tanımlar; CLAUDE.md *nasıl uygulanır* rehberidir,
bu belge *ne pazarlık edilemez* sorusunu yanıtlar. Çakışma olursa anayasa kazanır.

## Core Principles

### I. Bounded Context İzolasyonu (NON-NEGOTIABLE)

Her mikroservis tek bir Bounded Context'tir ve sınırı fiziksel olarak serttir.

- Her context'in kendi veritabanı, kendi şeması ve kendi domain modeli olmak ZORUNDADIR;
  paylaşılan ortak bir domain modeli OLMAMALIDIR.
- Bir servis, başka bir servisin veritabanına, tablosuna veya aggregate'ine doğrudan
  erişemez. Bu KESİN olarak yasaktır.
- Context'ler arası iletişim yalnızca (a) `Shared` içindeki bilinçli olarak paylaşılan
  integration event kontratları ve (b) anlık tutarlılık zorunlu olduğunda sanksiyonlu
  senkron gRPC üzerinden yapılır.
- Aynı kavram farklı context'te farklı modeldir (ör. "Merchant" Payment'ta sadece bir
  referans, MerchantManagement'ta zengin bir aggregate olabilir); model bir context'ten
  diğerine sızdırılMAZ.

Gerekçe: İzolasyon, servislerin bağımsız evrilmesini ve dağıtılmasını mümkün kılar;
paylaşılan veritabanı veya model, mikroservis mimarisini gizli bir monolite çevirir.

### II. Zengin Domain Modeli ve Invariant'lar

İş kuralları domain modelinin içinde yaşar, dışında değil.

- Anemik (davranışsız) aggregate YASAKTIR. Aggregate'ler private setter + statik `Create`
  fabrikası + davranış metotlarıyla yazılır.
- Aggregate root tutarlılık sınırıdır; dış dünya aggregate'i yalnız kök üzerinden değiştirir.
  Aggregate kökleri `AggregateRoot`'tan, kök olmayan ama kimlik/denetim alanı gerektiren
  entity'ler `BaseModel`'den türer.
- Invariant ihlalleri handler'da değil, aggregate metodunda yakalanır. Koleksiyonlar private
  tutulur ve yalnızca okunur olarak expose edilir; mutasyon yalnız aggregate metotlarından geçer.
- Kimliği olmayan kavramlar Value Object (private ctor + statik `Create`), tip-güvenli enum'lar
  `Enumeration` ile modellenir.

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

Gerekçe: Bir feature'ın tüm parçalarını tek dosyada tutmak, değişimi yerelleştirir ve
katmanlar-arası dağılmayı ortadan kaldırır.

### IV. Result Pattern (exception'suz beklenen hata akışı)

Beklenen hatalar (bulunamadı, doğrulama, iş kuralı ihlali) exception ile DEĞİL, Result
nesnesiyle taşınır.

- Handler'lar, aggregate metotları ve endpoint'ler `Common.Results` altındaki tiplerden
  (`FeatureObjectResultModel<T>`, `FeatureResultModel`, `ResultDomain` ...) birini döner.
  Bu tipler statik fabrika metotlarıyla üretilir, `new` ile değil.
- Hata bilgisi `MessageItem` ile taşınır; `Code` serbest metin DEĞİL, bir resource sabitidir
  (ör. `CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND`).
- Exception yalnızca gerçekten beklenmeyen durumlar içindir; onları `GlobalExceptionHandler`
  yakalar.

Gerekçe: Beklenen hataları tipli sonuçlarla taşımak akışı öngörülebilir kılar ve kontrol
akışının exception'lara bağlanmasını önler.

### V. Merkezi Kimlik ve Açık Yetki

Kimlik doğrulama merkezîdir ve hiçbir korunması gereken uç açıkta bırakılMAZ.

- Kimlik, OpenIddict tabanlı merkezi Identity servisi (`Identity.Server`, sabit issuer
  `https://localhost:5101`) tarafından verilir; servisler bu otoriteye göre (JWKS üzerinden,
  paylaşılan DB olmadan) kimlik doğrular.
- Durum değiştiren veya hassas veri döndüren her endpoint ve mesaj handler'ı, erişim için
  gereken yetkiyi AÇIKÇA beyan etmek ZORUNDADIR; "varsayılan açık" uç bırakılMAZ.
- Multitenant izolasyon korunur: bir merchant'ın verisi başka bir merchant'a asla sızmaz;
  sorgular tenant sınırıyla filtrelenir.
- Makine (M2M) düzleminde yetki modeli KARARLIDIR (011): client_credentials + scope-tabanlı
  policy; kural GET → `<bc>.read`, durum değiştiren → `<bc>.write`; access token'da scope
  claim'i JSON dizisidir. İnsan/rol düzlemi (G3) ve merchant-istemci düzlemi (G2) için
  bkz. TODO(AUTHZ_MODEL) — karar netleştiğinde amendment ile işlenir.

Gerekçe: Ödeme sistemi için yetki, sonradan eklenen değil baştan tasarlanan bir kısıttır;
modelin ayrıntısını ertelemek, "her erişim açıkça yetki gerektirir" kuralını ertelemez.

### VI. Spec-Driven Development

Önemsiz olmayan her değişiklik spec-kit akışından geçer.

- Akış: `/speckit-constitution` → `/speckit-specify` → (`/speckit-clarify`) → `/speckit-plan`
  → `/speckit-tasks` → `/speckit-implement`. Doğrudan koda atlamadan önce en azından spec üretilir.
- Anayasa her şeyin üstündedir. Bir spec, plan veya kod anayasayla çelişemez; çelişirse ya
  koda uydurulur ya da anayasa gerekçeli bir amendment ile güncellenir.
- Artefakt seti feature büyüklüğüne göre ölçeklenir: _trivial_ değişiklik spec'siz olabilir;
  _küçük_ feature yalnız `spec.md` + `tasks.md` üretir; _tam_ feature tam akıştan geçer.
  Şüphede kalınırsa bir üst kademe seçilir.

Gerekçe: Spec-driven akış, kararların kod yazılmadan önce yazılı ve gözden geçirilmiş olmasını
sağlar; anayasa bu akışa tutarlılık zemini verir.

## Teknoloji ve Alan Kısıtları

- **Platform:** .NET 10; her projede `Nullable` ve `ImplicitUsings` açıktır.
- **Orkestrasyon:** .NET Aspire. Sistem her zaman AppHost üzerinden ayağa kalkar; servisler
  bağımlılıklarını (Postgres, RabbitMQ, diğer servisler) Aspire service discovery + connection
  string enjeksiyonu ile bulur. Tek servisi izole çalıştırmak desteklenen bir yol değildir.
- **Kalıcılık:** Marten (Postgres üstünde document/event store); EF Core ile ilişkisel
  modelleme, izole altyapı servisleri (ör. Identity) dışında tercih edilmez.
- **Mesajlaşma:** Wolverine — süreç-içi command/query bus'ı ve RabbitMQ üstünden fanout
  integration mesajlaşması. Handler'lar assembly taramasıyla keşfedilir.
- **Paket yönetimi:** Central Package Management. Sürümler yalnız `Directory.Packages.props`'ta
  tanımlanır; `.csproj` dosyaları `PackageReference`'ı sürümsüz listeler. (Bilinçli istisna:
  `CP.VPOS`, CPM dışıdır ve kendi sürümlerini tutar.)
- **DI:** `ITransientDependency`/`IScopedDependency`/`ISingletonDependency` marker arayüzleri +
  Scrutor otomatik kaydı. Elle `services.Add...` yerine marker tercih edilir.
- **Alan kısıtı — yalnız TL:** Sistem yalnızca TL işlemi destekler; yabancı para birimi
  DESTEKLENMEZ. Para birimi çok-değerli bir alan olarak modellenmez.
- **CP.VPOS sınırı:** `CP.VPOS` sanal POS kütüphanesinin tipleri slice/domain sınırını GEÇMEZ;
  handler sınırında domain temsiline çevrilir.

## Geliştirme Akışı

- **Dil:** Kod yorumları, XML dokümanları, commit mesajları ve kullanıcıya dönük doğrulama
  mesajları Türkçe yazılır; mevcut konvansiyon korunur.
- **Using'ler:** Her projenin tek bir `GlobalUsings.cs`'i vardır; paylaşılan namespace'ler
  oraya eklenir.
- **Test:** Testler saf domain birim testleridir (host/entegrasyon harness'ı yok); banka/dış
  HTTP çağrıları test edilmez. Davranışlı aggregate'ler ve saf domain servisleri (ör. yönlendirme
  hesabı) test için önceliklidir.
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

**Version**: 1.1.0 | **Ratified**: 2026-07-29 | **Last Amended**: 2026-08-07