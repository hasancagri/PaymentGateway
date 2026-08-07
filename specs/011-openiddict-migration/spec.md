# Feature Specification: OpenIddict Migrasyonu + BC API Yetkilendirmesi

**Feature Branch**: `011-openiddict-migration`

**Created**: 2026-08-07

**Status**: Draft

**Input**: User description: "Duende→OpenIddict migrasyonu (G1): kimlik motoru OpenIddict'e geçer, ASP.NET Identity kalır;
BC API'lerine ilk kez kimlik doğrulama + scope yetkisi gelir; ECommerce'ten kopyalanan kimlik kalıntıları temizlenir."

## Bağlam ve Neden

- Repo'daki `Identity.Server`, ECommerce projesinden kopyalanmış Duende IdentityServer kurulumudur: uyur durumda
  (orkestrasyona dahil değil), scope/client seti başka projeye ait ve hiçbir BC API'si kimlik doğrulamaz.
- Duende ticari kullanımda lisans ücretlidir; proje kararı (2026-08-05) MIT lisanslı OpenIddict'tir. ECommerce 029
  migrasyonu birebir blueprint olarak mevcuttur.
- Bu feature, merchant onboarding yol haritasının (G1→G5) ilk adımı ve **prerequisite**'idir: G2 (merchant =
  client_credentials istemcisi) bu motorun üstüne kurulacaktır.
- Anayasa İlke V "hiçbir korunması gereken uç açıkta bırakılMAZ" der; bugün tüm uçlar açıktır. Bu feature ilkeyi
  ilk kez fiilen uygular. İlke V metni kimlik otoritesi olarak Duende'yi anar — implementasyonla birlikte gerekçeli
  bir anayasa amendment'ı gerekir (governance kuralına uygun).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kimlik motoru OpenIddict üzerinde token verir (Priority: P1)

Sistem yöneticisi (işletici) olarak, kimlik sağlayıcının ücretsiz lisanslı bir motorla çalışmasını istiyorum;
böylece ödeme ağ geçidi ticari lisans yükümlülüğü olmadan token verebilir.

**Why this priority**: Motor değişimi diğer her şeyin zeminidir; token verilemeden API koruması ve sonraki
merchant kimliği (G2) kurulamaz. Lisans riski tek başına migrasyon gerekçesidir.

**Independent Test**: Sistem ayağa kalkar; bir iç istemci, kimlik sunucusunun token ucundan makine token'ı alır;
token'ın issuer/scope/claim içeriği beklenen şekildedir. Duende'ye ait hiçbir sunucu bileşeni çalışmaz.

**Acceptance Scenarios**:

1. **Given** sistem orkestrasyonla ayakta, **When** tanımlı bir iç istemci client_credentials ile token isterse,
   **Then** geçerli bir access token döner ve token istenen scope'ları içerir.
2. **Given** tanımsız istemci kimliği veya yanlış secret, **When** token istenirse, **Then** istek reddedilir.
3. **Given** çözüm bağımlılık listesi, **When** kimlik sunucu paketleri incelenirse, **Then** ticari lisanslı
   kimlik sunucu bileşeni kalmamıştır.

---

### User Story 2 - BC API'leri açık yetkiyle korunur (Priority: P2)

İşletici olarak, ödeme/merchant/komisyon verilerine yalnız yetkili istemcilerin erişmesini istiyorum;
böylece ödeme sistemi verisi "varsayılan açık" uç üzerinden sızamaz.

**Why this priority**: İlke V'in fiili uygulaması; motor (US1) olmadan yapılamaz, ama migrasyonun asıl iş değeri
budur. Admin ekranlarının çalışmaya devam etmesi bu hikâyenin parçasıdır.

**Independent Test**: Token'sız doğrudan API çağrısı reddedilir; Admin ekranlarındaki mevcut akışlar (merchant,
banka, komisyon, settlement) davranış değişmeden çalışır.

**Acceptance Scenarios**:

1. **Given** korunan bir BC API ucu, **When** token'sız çağrılırsa, **Then** istek kimlik hatasıyla reddedilir.
2. **Given** geçerli ama ilgili scope'u taşımayan token, **When** korunan uç çağrılırsa, **Then** istek yetki
   hatasıyla reddedilir.
3. **Given** doğru scope'lu makine token'ı taşıyan Admin arayüzü, **When** mevcut ekran akışları
   (listele/oluştur/güncelle) kullanılırsa, **Then** tümü bugünkü davranışıyla çalışır.
4. **Given** durum değiştiren veya hassas veri döndüren herhangi bir uç, **When** uç tanımı incelenirse,
   **Then** gereken yetki uçta açıkça beyan edilmiştir (varsayılan-açık uç yok).

---

### User Story 3 - Agent akışı yetkili olarak sürer (Priority: P3)

E-ticaret tarafındaki alıcı ajanı adına çalışan sistem, taksit sorgu/seçim akışını (A2A → iç araç çağrıları)
kesintisiz sürdürebilmelidir; iç araç çağrıları artık yetkili kimlikle yapılır.

**Why this priority**: 007/024 akışları canlı değer; API koruması (US2) bu akışı kırmamalıdır. Dış A2A sınırının
kimliği bilinçli olarak sonraki fazlara (G2/G3, merchant kimliği) bırakılmıştır.

**Independent Test**: A2A üzerinden taksit sorgusu uçtan uca çalışır; ajanın iç araç çağrıları token taşır.

**Acceptance Scenarios**:

1. **Given** sistem ayakta ve API'ler korunuyor, **When** A2A taksit sorgu akışı koşulursa, **Then** akış uçtan
   uca başarılı tamamlanır.
2. **Given** ajan istemcisinin token edinemediği bir durum, **When** iç araç çağrısı yapılırsa, **Then** çağrı
   reddedilir ve akış anlaşılır bir hata ile sonlanır (sessiz başarı yok).

---

### Edge Cases

- Kimlik sunucusu kapalıyken BC API çağrısı gelirse: daha önce doğrulanmış imza anahtarlarıyla token doğrulaması
  sürebilir; yeni token verilemez — Admin/ajan akışı anlaşılır hata gösterir.
- Süresi dolmuş token ile çağrı: kimlik hatasıyla reddedilir; istemci yeni token edinir (tekrar kullanılabilir akış).
- Birden çok scope taşıyan token'da tek scope kontrolü: çoklu-scope token doğru değerlendirilir (ECommerce 029'da
  yaşanan "scope tek string kaldı" hatası burada tekrarlanmaz — kabul senaryosu olarak test edilir).
- Kopyalanan eski kimlik kalıntısına (UserKey/ApiKeys) ait uç veya veri talebi: sistemde böyle bir yüzey kalmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Kimlik sunucusu OpenIddict motoruna geçer; kullanıcı deposu olarak ASP.NET Identity KALIR.
- **FR-002**: Ticari lisanslı kimlik sunucu bileşenleri (Duende server paketleri) çözümden tamamen çıkarılır.
- **FR-003**: Scope seti bu repo'nun BC'lerine göre tanımlanır: `payment.read/write`, `merchant.read/write`,
  `commission.read/write`. ECommerce'e ait scope seti silinir. (Reference BC'nin HTTP yüzeyi yoktur —
  010'da event-only pivotu; scope tanımlanmaz.)
- **FR-004**: İki iç makine istemcisi tanımlanır: Admin arayüzü (BFF) ve Payment ajanı; her biri yalnız ihtiyaç
  duyduğu scope'ları alabilir (least-privilege).
- **FR-005**: HTTP yüzeyi olan üç BC API'si (Payment, Merchant, Commission) gelen istekte kimlik doğrular;
  durum değiştiren veya hassas veri döndüren her uç gereken scope'u açıkça beyan eder.
- **FR-006**: Admin arayüzü API çağrılarına makine token'ı ekler; mevcut tüm ekran akışları davranış değişmeden
  çalışır. İnsan login'i ve rol tabanlı yetki bu feature'ın DIŞINDADIR (ayrı feature).
- **FR-007**: Payment ajanının iç araç çağrıları makine token'ı taşır; A2A taksit akışı (007/024) uçtan uca
  çalışmaya devam eder. Dış A2A sınırının kimliği bu feature'ın DIŞINDADIR (G2/G3).
- **FR-008**: ECommerce'ten kopyalanan UserKey/ApiKeys alt sistemi ve ilgili kimlik kalıntıları (uçlar, veri
  modeli, doğrulama bileşenleri, kullanılmayan etkileşim sayfaları) silinir.
- **FR-009**: Kimlik sunucusu sistem orkestrasyonuna dahil edilir ve sistemle birlikte ayağa kalkar; tüm
  servisler kimlik otoritesini tutarlı tek adresten tanır.
- **FR-010**: Çoklu-scope token'larda scope değerlendirmesi doğru çalışır (tek-scope ve çok-scope token'lar aynı
  yetki sonucunu verir).
- **FR-011**: Token veren istemci kimlik bilgileri (secret'lar) kaynak koda gömülmez; yapılandırma/gizli-değer
  mekanizmasından okunur.

### Key Entities

- **İç istemci (client)**: Token alabilen makine kimliği; kimlik, secret ve izinli scope listesi taşır.
  Bu feature'da: Admin arayüzü ve Payment ajanı.
- **Scope**: BC başına okuma/yazma yetki birimi; korunan uçların beyan ettiği erişim sözleşmesi.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Token'sız veya yetkisiz doğrudan çağrıda hiçbir korunan uç veri döndürmez (korunan uçların %100'ü).
- **SC-002**: Mevcut Admin ekran akışlarının tamamı (merchant/banka/komisyon/settlement listeleme-oluşturma-
  güncelleme) migrasyon sonrası davranış değişmeden tamamlanır.
- **SC-003**: A2A taksit sorgu/seçim akışı uçtan uca başarılı tamamlanır.
- **SC-004**: Çözümde ticari lisans gerektiren kimlik sunucu bileşeni sayısı 0'dır.
- **SC-005**: Kopyalanan eski kimlik yüzeyine (UserKey/ApiKeys) ait erişilebilir uç sayısı 0'dır.

## Assumptions

- İnsan login'i ve RBAC (rol=payment-admin vb., 2026-08-05 kararının rol kısmı) AYRI feature'dır; bu feature
  yalnız makine düzlemini kurar. Admin arayüzü o feature'a kadar makine token'ıyla çalışır.
- Merchant kimliği (client_id=merchantId, MerchantKey=secret, status-gated scope) G2'nin konusudur; bu feature
  yalnız iki iç istemciyi tanımlar.
- Kimlik veritabanı ilk kez canlı kullanılacaktır; taşınacak kullanıcı/izin verisi yoktur — temiz kurulum kabul
  edilir (migration geçmişi devralınmaz).
- ECommerce 029 migrasyonu davranış-birebir blueprint'tir; oradaki bilinen tuzaklar (çoklu-scope claim davranışı,
  kimlik sunucusunun güvenli bağlantı/issuer tutarlılığı gereksinimi) bu kurulumda baştan dikkate alınır.
- YARP gateway'deki mevcut kimlik extension çağrısı bu feature kapsamında gözden geçirilir; koruma asıl olarak
  BC API'lerinde uygulanır.