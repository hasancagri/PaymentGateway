# Feature Specification: NonSecure Banka Çekim Dilimi

**Feature Branch**: `090-nonsecure-bank-charge`

**Created**: 2026-10-21

**Status**: Draft

**Input**: User description: "090 NonSecure banka çekim dilimi — PaymentLink 'Öde' stub'ının gerçek NonSecure banka çekimiyle değişmesi" (onaylı plan `~/.claude/plans/mutable-snuggling-bentley.md` + EventStorming `docs/eventstorming/payment-pay-by-link.md` Perde-2 Banka Sahnesi)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Müşteri linkten öder, çekim başarılı (Priority: P1)

Müşteri, store agent'ının sohbette verdiği ödeme linkini açar, güven bağlamını (mağaza + sipariş + tutar) görür, kart bilgisini hosted sayfada girer ve "Öde"ye basar. Çekim bir bankaya (bu versiyonda simüle) gider, banka onaylar, link ödendi (Paid) olur. Müşteri başarı görür; store ödeme sonucunu otomatik, güvenilir (imzalı) bir bildirimle öğrenir.

**Why this priority**: Pay-by-link akışının asıl değeri — para gerçekten tahsil edilir ve store siparişi "ödendi"ye çevirebilir. Bu olmadan link üretimi (089) eksik bir iskelet.

**Independent Test**: Geçerli açık bir link + onay dönen test kartıyla "Öde" çağrılır; link Paid'e geçer, bir başarılı deneme kaydı tutulur, store bir başarı bildirimi + ödeme-tamamlandı olayı alır. Tek başına MVP değer sunar.

**Acceptance Scenarios**:

1. **Given** açık (Opened) ve süresi geçmemiş bir link, **When** müşteri onay dönen kartla "Öde"ye basar, **Then** çekim onaylanır, link Paid terminaline geçer, bir başarılı deneme kaydı eklenir, store imzalı başarı bildirimi + ödeme-tamamlandı olayı alır.
2. **Given** çekim onaylandı, **When** müşteri aynı link için tekrar "Öde"ye basar, **Then** sistem ikinci çekim yapmaz (Paid terminal, tek-kullanım), çift çekim olmaz.
3. **Given** çekim başarılı, **When** müşteri başarı sayfasını görür, **Then** bu sayfa yalnızca bilgilendirmedir; kesin onay store'a giden imzalı bildirimdir (tarayıcı ekranı kanıt değildir).

---

### User Story 2 - Çekim issuer tarafından reddedilir (Priority: P2)

Müşteri kartını girip "Öde"ye basar, ancak kartı basan banka işlemi reddeder (ör. yetersiz bakiye, çalıntı kart, hatalı CVV). Müşteriye başka bir kartla tekrar deneyebileceği anlaşılır bir mesaj gösterilir; link açık kalır (terminale geçmez). Store ödemenin başarısız olduğunu öğrenir.

**Why this priority**: Gerçek hayatta çekimlerin önemli kısmı reddedilir; red doğru sınıflanmazsa müşteri sıkışır, store yanlış bilgilenir, çift çekim riski doğar.

**Independent Test**: Red dönen test kartıyla "Öde" çağrılır; link Paid'e geçmez, bir başarısız deneme (red sınıfıyla) kaydedilir, müşteri normalize edilmiş red mesajı görür, store başarısızlık bildirimi alır. Link aynı müşteri tarafından başka kartla yeniden denenebilir.

**Acceptance Scenarios**:

1. **Given** açık bir link, **When** müşteri issuer-reddi dönen kartla "Öde"ye basar, **Then** link açık kalır, başarısız deneme (IssuerDecline sınıfı) kaydedilir, müşteriye "başka kartla deneyin" mesajı verilir, store başarısızlık bildirimi alır.
2. **Given** issuer reddi alındı, **When** sistem sonucu değerlendirir, **Then** aynı sipariş başka bir bankaya yönlendirilmez (aynı issuer yine reddeder) — müşterinin başka kart (farklı issuer) denemesi beklenir.
3. **Given** red dolandırıcılık (fraud) sınıfındaysa, **When** sonuç değerlendirilir, **Then** işlem asla otomatik yeniden denenmez.

---

### User Story 3 - Teknik hatada başka bankaya canlı geçiş (Priority: P3)

Çekim sırasında yönlendirilen banka teknik olarak cevap veremezse (zaman aşımı, bağlantı/5xx, tanım hatası), sistem sıradaki uygun bankaya **canlı** geçer. Bu versiyonda 3 simüle banka canlı olduğundan geçiş fiilen tetiklenir ve onaya ulaşana dek sürer.

**Why this priority**: Çok-bankalı dayanıklılığın asıl değeri — bir banka düşse bile ödeme tamamlanır. Bu versiyonda gerçek failover yolu canlı koşar.

**Independent Test**: 3 banka canlıyken ilki teknik hata, ikincisi onay dönecek şekilde ayarlanır; çekim ikinci bankada onaylanır. Issuer/fraud reddinde geçiş DURUR. İlk uygun banka onaylarsa kalan bankalar çağrılmaz.

**Acceptance Scenarios**:

1. **Given** sıralı bankalardan ilki teknik hata dönüyor, **When** çekim yönlendirilir, **Then** sıradaki uygun banka denenir ve onaylanırsa link Paid olur.
2. **Given** bir banka issuer-reddi veya fraud-reddi dönüyor, **When** çekim yönlendirilir, **Then** sıradaki banka DENENMEZ, red olduğu gibi döner.
3. **Given** ilk uygun banka onaylıyor, **When** çekim yönlendirilir, **Then** kalan bankalar hiç çağrılmaz.
4. **Given** tüm uygun bankalar teknik hata dönüyor, **When** liste tükenir, **Then** sistem normalize "sağlayıcı kullanılamıyor" döndürür, link Paid'e geçmez.

---

### User Story 4 - Akıllı yönlendirme: yetenek eleme + on-us (Priority: P2)

Çekim 3 bankaya körlemesine sırayla gitmez; önce kartın/işlemin gereğini karşılayan bankalar elenir (yetenek), sonra kartın issuer'ıyla aynı bankaya (on-us) ve ardından maliyet önceliğine göre sıralanır. Kart BIN'inden issuer/şema/kart tipi belirlenir.

**Why this priority**: Doğru banka seçimi başarı oranını ve maliyeti belirler; on-us işlem hem daha ucuz hem daha yüksek onaylıdır (İlke VII: yetenek eleme + fiyat + on-us).

**Independent Test**: Bir kart BIN'i X bankasının issuer'ına çözülür; 3 banka içinde X acquirer'ı varsa on-us olarak önce o denenir. Bir kart tipini desteklemeyen banka aday listesinden elenir.

**Acceptance Scenarios**:

1. **Given** kart BIN'i bir acquirer bankanın issuer'ına çözülüyor, **When** yönlendirme sıralanır, **Then** o banka (on-us) öncelik alır.
2. **Given** bir banka bu kart tipini/işlemi desteklemiyor, **When** aday listesi kurulur, **Then** o banka elenir (denenmez).
3. **Given** birden çok uygun banka var ve on-us eşleşme yok, **When** sıralanır, **Then** maliyet önceliğine göre sıralanır.
4. **Given** BIN bilinmiyor (BinCard'da yok), **When** yönlendirme kurulur, **Then** on-us uygulanmaz, uygun bankalar maliyet sırasıyla denenir (fail-open routing, çekim engellenmez).

---

### Edge Cases

- **Çift "Öde" / çift gönderim**: Aynı link iki kez ödenemez; Paid'e ulaşmış link yeni çekim tetiklemez (tek-kullanım, terminal tek-yön).
- **Ödeme sırasında süre dolumu**: "Öde" anında link TTL+grace dışına düşmüşse çekim yapılmaz, link süresi-geçti (Expired) kabul edilir.
- **Terminal link'e ödeme**: İptal/Expired/Paid link'e "Öde" reddedilir (terminal ihlali).
- **Tüm bankalar teknik hata**: Çekim başarısız sayılır; link açık kalır veya normalize red döner; çift çekim yaratmadan.
- **Çift-çekim penceresi (bilinçli, belgeli)**: Teknik hatada bir banka aslında çekmiş olabilir; durum-sorgu bu versiyonda YOK → sıradaki bankaya geçiş çift-çekim penceresi açar. Simülatörde bakiye/para olmadığı için şu an ZARARSIZ; GERÇEK banka öncesi durum-sorgu (timeout-güvenli failover) ZORUNLUDUR — sonraki versiyon. FLOW.md + router özetinde belgeli.
- **BIN bilinmiyor**: BinCard'da olmayan BIN → on-us uygulanmaz, routing fail-open (uygun bankalar maliyet sırasıyla), çekim engellenmez.
- **Kart verisi sızıntısı**: Kart numarası/CVV hiçbir kalıcı yerde (kayıt/log/dayanıklı kuyruk/dönüş verisi) görünmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, açık (Pending/Opened) ve süresi (TTL+grace) geçmemiş bir ödeme linki için müşterinin girdiği kartla bir banka çekimi başlatabilmelidir (NonSecure, senkron).
- **FR-002**: Çekim onaylandığında sistem link'i Paid terminaline taşımalı ve bu geçiş tek-yönlü, tek-kullanımlık olmalıdır (Paid link tekrar çekim tetikleyemez).
- **FR-003**: Sistem her çekim denemesini (hangi banka, sonuç, red sınıfı, banka referansı, onay kodu, zaman) bir deneme kaydı olarak link altında tutmalı; bir link altında birden çok deneme olabilmelidir. Deneme kayıtları denetim/mutabakat için saklanır.
- **FR-004**: Sistem banka sonuçlarını normalize bir sözlüğe çevirmelidir: onaylı; ya da reddedilmiş + red sınıfı ∈ {IssuerDecline, TechnicalFailure, ConfigError, FraudDecline}. Red sınıfı banka-özel ham koddan türetilir (her banka kendi ham kodunu çevirir).
- **FR-005**: Sistem, yalnız TechnicalFailure ve ConfigError durumlarında sıradaki uygun bankaya geçecek (failover); IssuerDecline ve FraudDecline'da geçiş YAPMAMALIDIR (aynı issuer boşuna denenir; fraud asla yeniden denenmez).
- **FR-006**: Sistem onay dönene kadar ya da başka-bankaya-geçilemez bir redde (Issuer/Fraud) kadar uygun bankaları sırayla deneyebilmeli; tümü teknik hata verirse normalize "sağlayıcı kullanılamıyor" döndürmelidir.
- **FR-007**: Sistem 3 in-process simüle bankayla canlı çalışmalı; her simüle banka, hesap/bakiye modeli olmadan, kart/tutar tabanlı sihirli test-veriyle onay/red/teknik-hata sonucu üretir. Failover gerçek yolla koşar; çift-çekim emniyeti (durum-sorgu) GERÇEK banka öncesi zorunludur (sonraki versiyon, belgeli).
- **FR-015**: Sistem kart BIN'inden issuer banka + şema + kart tipini belirleyebilmeli (BIN→kart bilgisi çözümü); bilinmeyen BIN routing'i engellemez (fail-open).
- **FR-016**: Sistem yönlendirmeyi iki aşamada yapmalı: (1) yetenek eleme — kartı/işlemi desteklemeyen bankalar aday listesinden çıkar; (2) sıralama — on-us (kart issuer'ı == acquirer bankası) önce, sonra maliyet önceliği. Failover bu sıralamayı izler.
- **FR-017**: Her acquirer için yönlendirme öngörü verisi (banka kodu, desteklenen yetenek, maliyet/öncelik) yapılandırmadan okunmalı; bu veri PG'de bir ÖNGÖRÜ kopyasıdır (banka otorite, İlke VII).
- **FR-018**: BIN→kart-bilgisi referans verisi sistemde tutulmalı ve güncellenebilir olmalı; bu versiyonda statik seed'den yüklenir (güncelleme = reseed), canlı besleme kaynağı sonraki versiyona bırakılır.
- **FR-008**: Hosted ödeme sayfası, güven bağlamından sonra kart alanlarını (numara, sahip, son kullanım, CVV) toplamalı; bu veriler yalnız çekim anında bankaya iletilmeli, PG tarafından HİÇBİR kalıcı ortamda saklanmamalıdır (anayasa İlke VI).
- **FR-009**: Terminal sonuçta (başarılı veya başarısız) sistem store'a imzalı (HMAC) ve dayanıklı (yeniden-denemeli) bir sonuç bildirimi göndermelidir; imza ham gövde üzerinden üretilir, store aynı ham gövdeyi doğrular.
- **FR-010**: Terminal sonuçta sistem ilgili ödeme olayını (tamamlandı / başarısız) yayınlamalıdır (tüketici Order BC geldiğinde bağlanır).
- **FR-011**: Çekim/bildirim durumunu değiştiren işlem atomik olmalı: link durum değişikliği, olay yayını ve store bildirimi aynı işlemde (ya hep ya hiç) gerçekleşmelidir.
- **FR-012**: Müşteriye gösterilen red mesajları ve store'a dönen hata kodları serbest metin değil, sabit kaynak kodları olmalıdır (red sınıfından türetilir).
- **FR-013**: Kart verisi dayanıklı mesaj/kuyruk yüküne girmemeli (çekim senkron işlenir, kart verisi dayanıklı depoya yazılmaz).
- **FR-014**: Store'un çekim başlatma yetkisi bu versiyonda mevcut geçici `payment.write` yetkisiyle korunur; müşteri açılış/ödeme ucu kimliksiz ama gizli-token kapılı (fail-closed) kalır. (Nihai platform IdP yetkisi sonraki versiyon.)

### Key Entities *(include if feature involves data)*

- **Ödeme Linki (PaymentLink)**: Mevcut aggregate genişler — artık ödeme modeli alanı (bu versiyonda NonSecure) ve deneme geçmişi taşır; Paid terminaline çekim başarısıyla ulaşır.
- **Ödeme Denemesi (PaymentAttempt)**: Bir link altındaki tek bir çekim girişimi — hangi banka, sonuç (onay/red), red sınıfı, banka referansı, onay kodu, zaman. Kart verisi İÇERMEZ.
- **Banka Çekim Sonucu (normalize)**: Banka cevabının anti-corruption normalize şekli — onaylı/reddedilmiş + red sınıfı + banka referansı + onay kodu + ham kod (denetim).
- **Banka Adaptörü (port)**: Her banka, ortak bir port arkasında kendi istek/cevap çevirisini (wire → normalize sonuç) yapan bir adaptör; bu versiyonda 3 in-process simüle adaptör.
- **Acquirer Profili (PosAccount/BankProfile)**: Bir acquirer bankanın yönlendirme öngörü verisi — banka kodu, desteklenen yetenekler (kart tipi/işlem), maliyet/öncelik. PG'de öngörü kopyası (banka otorite, İlke VII). Yapılandırmadan okunur.
- **BIN Kartı (BinCard)**: BIN → issuer banka + şema + kart tipi referans kaydı; yönlendirmede issuer tespiti + on-us eşlemesi için kullanılır. Statik seed'den yüklenir.
- **Store Sonuç Bildirimi**: Terminal sonucu store'a taşıyan imzalı, dayanıklı bildirim (ham gövde imzalanır).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Açık bir link + onay dönen kartla müşteri tek "Öde" aksiyonunda ödemeyi tamamlar; link Paid olur ve store sonucu 100% vakada otomatik (manuel adım olmadan) öğrenir.
- **SC-002**: Reddedilen her çekimde link Paid'e GEÇMEZ ve müşteri başka kartla yeniden deneyebilir; red sonrası çift çekim 0 vakada oluşur.
- **SC-003**: Red sınıflama doğruluğu: issuer reddi başka bankaya yönlendirilmez, teknik hata yönlendirilir, fraud asla yeniden denenmez — tanımlı test senaryolarının 100%'ünde doğru karar.
- **SC-004**: Kart verisi (numara/CVV) hiçbir kalıcı ortamda (kayıt/log/dayanıklı kuyruk/dönüş verisi) görünmez — denetimde 0 sızıntı.
- **SC-005**: Store'a giden terminal bildirimi imzalıdır ve geçici teslim hatasında yeniden denenir; store ham gövdeyi aynı imzayla doğrulayabilir (doğrulama 100% vakada geçer).
- **SC-006**: Bir banka teknik hata verdiğinde çekim onay dönene kadar sıradaki uygun bankada denenir; 3 bankadan en az biri onay veriyorken ödeme 100% vakada tamamlanır (canlı failover).
- **SC-007**: Yönlendirme doğruluğu: kart issuer'ıyla eşleşen acquirer (on-us) önce denenir, desteklemeyen banka elenir — tanımlı routing test senaryolarının 100%'ünde doğru aday + sıra.

## Assumptions

- Store→gateway çekim başlatma yetkisi bu versiyonda mevcut geçici `payment.write` JWT scope ile korunur; nihai platform IdP yetkisi sonraki versiyona bırakılmıştır.
- 3 simüle banka gerçek acquirer'ların yerine geçer; çekim senkrondur (NonSecure), 3DS/ACS/OTP yoktur. Ayrı banka simülatör repo'su sonra in-process fake'lerin yerini alacaktır.
- Routing yetenek-eleme + on-us + maliyet sıralamasıyla sınırlıdır; devre-kesici/sağlık-state + çok gelişmiş fiyat matrisi sonraki versiyona bırakılır (seam).
- BinCard verisi statik seed'den yüklenir (dev; güncelleme = reseed); küçük sim BIN seti; canlı BIN besleme kaynağı ertelenir.
- Failover canlı koşar; durum-sorgu olmadığı için çift-çekim penceresi açıktır ama simülatörde bakiye olmadığından zararsızdır; GERÇEK banka öncesi durum-sorgu zorunlu (sonraki versiyon).
- Red taksonomisi dört sınıfla sınırlıdır (Issuer/Technical/Config/Fraud); daha ince ayrım gerekirse sonraki versiyonda genişler.
- Store bildirim imza anahtarı (CallbackSecret) secret-store'da tutulur; non-secret yapılandırmayla karışmaz.
- Store→gateway yetkisi geçici `payment.write`; nihai platform IdP sonraki versiyon.
- Para birimi yalnız TRY (089'dan devralındı).

## Out of Scope *(sonraki versiyon[ler])*

- 3DS / ACS ekranı / SMS-OTP; frictionless ve simüle ACS akışı.
- Asenkron sağlayıcı callback ucu (3DS dönüşü).
- Durum-sorgu (inquiry) + mutabakat/watchdog dayanıklı süreç — dolayısıyla **timeout-güvenli** failover (gerçek banka öncesi zorunlu).
- Banka hesap/bakiye modeli; gerçek kart şeması / BKM / issuer ağ gerçekçiliği; settlement/clearing.
- Devre-kesici/banka sağlık-state routing; **BinCard canlı besleme kaynağı** (bu versiyonda BinCard statik seed'le gelir, güncelleme = reseed).
- Nihai platform IdP yetki modeli; iade/void; klasik SMS/email link teslimi.
