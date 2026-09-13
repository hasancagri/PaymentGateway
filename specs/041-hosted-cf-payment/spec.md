# Feature Specification: Hosted-CF Ödeme Yüzeyi (iyzico Checkout Form)

**Feature Branch**: `041-hosted-cf-payment`

**Created**: 2026-09-13

**Status**: Draft

**Input**: PG, merchant sitesine (store) hosted ödeme akışı sunar: ödeme linki üret → müşteri iyzico hosted sayfada öder → sonuç imzalı callback ile store'a bildirilir. iyzico Checkout Form (sandbox) ile gerçek entegrasyon.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Hosted ödeme linki üretimi (Priority: P1)

Store (merchant sitesi), bir sipariş için müşterisine gösterilecek bir ödeme bağlantısı ister. PG, ödemeyi iyzico hosted ödeme formunda başlatır ve store'a bir hosted ödeme URL'si + PG ödeme referansı döner. Müşteri bu URL'de kartıyla öder; kart bilgisi hiçbir zaman store'a veya PG'ye girmez (yalnız iyzico hosted sayfada).

**Why this priority**: Ödeme akışının giriş kapısı; bu olmadan hiçbir ödeme başlayamaz. Tek başına MVP.

**Independent Test**: Geçerli merchant kimliğiyle ödeme başlatma isteği gönderilir → çalışan bir hosted ödeme URL'si + PG referansı döner; URL tarayıcıda iyzico ödeme formunu açar.

**Acceptance Scenarios**:

1. **Given** aktif bir merchant + geçerli tutar/sipariş referansı/dönüş adresi, **When** ödeme başlatma isteği gelir, **Then** hosted ödeme URL'si + PG ödeme referansı döner ve girişim "beklemede" saklanır.
2. **Given** aynı sipariş referansıyla ikinci kez başlatma isteği, **When** girişim hâlâ beklemede, **Then** yeni bir çift kayıt oluşmaz (aynı referans tekildir).
3. **Given** geçersiz/eksik merchant kimliği, **When** istek gelir, **Then** istek reddedilir ve ödeme başlatılmaz.

---

### User Story 2 - Ödeme sonucunun store'a bildirimi (Priority: P1)

Müşteri iyzico hosted sayfada ödemeyi tamamladığında (başarılı veya başarısız), iyzico sonucu PG'ye bildirir. PG sonucu doğrular ve store'un belirttiği dönüş adresine **imzalı** bir sonuç bildirimi (başarılı/başarısız + referanslar) gönderir; ardından müşterinin tarayıcısını bir dönüş sayfasına yönlendirir ("ödemen alındı / başarısız — işlemine dönebilirsin").

**Why this priority**: Sonuç bildirimi olmadan store siparişi tamamlayamaz/iptal edemez; akışın kapanışı.

**Independent Test**: Beklemedeki bir girişim için iyzico dönüşü simüle edilir → store'un dönüş adresine imzalı sonuç bildirimi gider (başarı ya da başarısızlık) ve müşteriye dönüş sayfası gösterilir.

**Acceptance Scenarios**:

1. **Given** başarılı ödemesi olan beklemedeki bir girişim, **When** iyzico dönüşü işlenir, **Then** girişim "başarılı" olur, store'a imzalı "başarılı" bildirimi gider ve müşteri başarı dönüş sayfasını görür.
2. **Given** başarısız/iptal ödeme, **When** iyzico dönüşü işlenir, **Then** girişim "başarısız" olur, store'a imzalı "başarısız" bildirimi (sebep kodu ile) gider ve müşteri başarısız dönüş sayfasını görür.
3. **Given** store dönüş adresine bildirim anlık ulaşmadı, **When** gönderim başarısız olur, **Then** sonuç PG'de kalıcıdır ve yeniden gönderilebilir (kayıp olmaz).

---

### User Story 3 - Güvenlik + tekrar-dayanıklılık (Priority: P2)

Store, aldığı sonuç bildiriminin gerçekten PG'den geldiğini doğrulayabilmelidir; sahte "ödendi" bildirimleri kabul edilmemelidir. Ayrıca aynı iyzico dönüşü birden çok kez gelse dahi sonuç tek olmalı, store'a çift bildirim gitse dahi ödeme tek sayılmalıdır.

**Why this priority**: Ödeme bütünlüğü + para güvenliği; MVP satışı P1 ile yapılır, bu sertleştirmedir.

**Independent Test**: (a) Store, imzayı yanlış anahtarla doğrularsa bildirimi reddeder. (b) Aynı girişim için iyzico dönüşü iki kez işlenir → ikinci işlem durumu değiştirmez, ikinci store bildirimi de aynı sonucu taşır.

**Acceptance Scenarios**:

1. **Given** store'a giden sonuç bildirimi, **When** bildirim imzalanır, **Then** imza store'un paylaştığı callback gizli anahtarıyla doğrulanabilir; anahtarsız/yanlış imza store tarafında reddedilir.
2. **Given** "başarılı" olmuş bir girişim, **When** aynı iyzico dönüşü tekrar gelir, **Then** durum değişmez (başarılıdan geri dönülmez) ve tekrar-bildirim aynı sonucu taşır.

---

### Edge Cases

- Beklemedeki bir girişimin süresi dolarsa (müşteri hiç ödemezse): iyzico dönüşü hiç gelmez → girişim beklemede kalır; store kendi tarafında terk-timeout ile iptal eder (PG store'a proaktif "terk" bildirimi göndermez — kapsam dışı, v1).
- Bilinmeyen/eşleşmeyen sipariş referansıyla iyzico dönüşü: PG bildirimi işleyemez → loglar, store'a bildirim gitmez.
- iyzico sonuç sorgusu (retrieve) geçici erişilemez: dönüş işleme yeniden denenebilir; girişim beklemede kalır.
- Merchant aktif değil (Provisioning/Passive/Suspended): ödeme başlatma reddedilir (charge yetkisi yalnız Active).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, kimliği doğrulanmış bir merchant için tutar + sipariş referansı + dönüş adresi alarak hosted bir ödeme başlatMALI ve çalışan bir hosted ödeme URL'si + PG ödeme referansı dönMELİdir.
- **FR-002**: Ödeme başlatma yalnız **Active** merchant için yapılMALI; charge yetkisi olmayan statüde (Provisioning/Passive/Suspended) reddedilMELİdir (anayasa İlke V, fail-closed).
- **FR-003**: Sistem her ödeme girişimini (sipariş referansı, iyzico ödeme referansı, tutar, merchant, store dönüş adresi, durum) kalıcı saklaMALIdır.
- **FR-004**: Sipariş referansı bir merchant kapsamında **tekil** olMALI; aynı referansla ikinci başlatma yeni çift kayıt üretMEMELİdir.
- **FR-005**: Müşteri iyzico hosted sayfada ödemeyi tamamladığında sistem sonucu iyzico'dan doğrulaMALI (başarı/başarısızlık + sebep) ve girişimi buna göre terminal duruma taşıMALIdır.
- **FR-006**: Sistem, sonucu store'un başlatmada verdiği dönüş adresine bir sonuç bildirimi (sipariş referansı, PG ödeme referansı, durum, başarısızsa sebep kodu) olarak gönderMELİdir.
- **FR-007**: Store'a giden her sonuç bildirimi, store'un doğrulayabileceği şekilde **imzalanMALI**dır (paylaşılan callback gizli anahtarı; giden istek kimlik anahtarından — MerchantKey — ayrı).
- **FR-008**: Sonuç işleme **idempotent** olMALI: aynı iyzico dönüşü birden çok kez gelse dahi girişim tek sonuç taşıMALI; terminal durumdan geri dönülMEMELİdir (başarılı → başarılı kalır).
- **FR-009**: Store dönüş adresine bildirim anlık ulaşmazsa sonuç PG'de **kaybolmaMALI**; yeniden gönderilebilir olMALIdır.
- **FR-010**: Sistem, ödeme tamamlandıktan sonra müşterinin tarayıcısını sonuca uygun bir **dönüş sayfasına** (başarılı/başarısız) yönlendirMELİdir.
- **FR-011**: Kart verisi (PAN/CVV) hiçbir aşamada store'a veya PG'ye girMEMELİ; yalnız iyzico hosted sayfada işlenMELİdir.
- **FR-012**: Sistem yalnız **TL** işlem destekleMELİdir (anayasa alan kısıtı).

### Key Entities *(include if data involved)*

- **Ödeme Girişimi (HostedPaymentSession)**: Bir hosted ödeme denemesini temsil eder. Nitelikler: sipariş referansı (store TxRef; merchant kapsamında tekil), iyzico ödeme referansı/token, tutar, merchant kimliği, store dönüş adresi, durum (beklemede/başarılı/başarısız), başarısızlık sebebi. Yaşam döngüsü: başlat → (başarılı | başarısız).
- **Sonuç Bildirimi (store callback)**: Store'a giden imzalı bildirim; sipariş referansı + PG ödeme referansı + durum + sebep kodu taşır; imza ile kökeni doğrulanır.
- **Merchant (referans)**: Yalnız kimlik + statü referansı (charge yetkisi Active kapılı); zengin aggregate Merchant BC'de.

## Success Criteria *(mandatory)*

- **SC-001**: Ödeme başlatma isteği normal koşulda 5 saniyeden kısa sürede çalışan bir hosted ödeme URL'si döner.
- **SC-002**: Başarılı ödemede store, sonuç bildirimini ödeme tamamlanmasından sonra alır ve imzayı %100 doğrulayabilir.
- **SC-003**: Aynı sipariş referansı için tekrar başlatma ve aynı iyzico dönüşünün tekrarı, çift ödeme kaydı veya çift terminal sonuç ÜRETMEZ (idempotency %100).
- **SC-004**: Sahte/yanlış-imzalı sonuç bildirimi store tarafında %100 reddedilir.
- **SC-005**: Kart verisi PG loglarında/veritabanında hiçbir yerde görünmez (PAN sızıntısı sıfır).

## Assumptions

- **iyzico Checkout Form (sandbox)** kullanılır; PG, iyzico ile initialize + retrieve wire'ını gerçek konuşur (mevcut V2 transport repurpose). Canlı/prod anahtar geçişi kapsam dışı.
- **CallbackSecret v1 = paylaşılan PG config** anahtarıdır (store'un doğrulama anahtarıyla eşleşir); per-merchant üretim/rotasyon kapsam dışı (backlog).
- Store, ödeme başlatmada kendi **dönüş adresini** (callback URL) verir; PG bu adrese sonuç bildirimi yollar. iyzico'nun kendi dönüş adresi PG'nindir (iyzico → PG).
- Sipariş referansının tekilliğini store üretir/garanti eder; PG merchant kapsamında tekillik zorlar.
- Terk (müşteri hiç ödemez) tespiti **store tarafında** yapılır (PG proaktif terk bildirimi göndermez, v1).
- İade/void, ödeme-durum sorgu ucu (poll-yedek), per-merchant secret rotasyon ve merchant onboarding elden geçirme **kapsam dışıdır**.
- ECom karşı-kontratı: `ECommerceWithAgentFramework/specs/077-hosted-cf-payment/contracts/pg-external.md` (store'un beklediği şekiller).