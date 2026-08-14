# Feature Specification: Kayıtlı Kartla Ödeme (NonSecure, Taksitli)

**Feature Branch**: `033-saved-card-payment`

**Created**: 2026-08-14

**Status**: Draft

**Input**: User description: "Kayıtlı kartla ödeme (createPayment NonSecure, taksitli, facilitator split YOK): gateway'in ilk gerçek çekim yeteneği. 032'nin iyzico Saklı Kart altyapısı üstüne oturur — CVC gerekmez. Taksit sorgu + çekim; başarıda Payment kaydı + PaymentCompletedEvent. Yeni payment.charge scope (Active-only). Efektif komisyon Payment BC'de hesaplanmaz (event-driven, Commission BC). Sub-merchant split yok."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gateway Çekim (Kayıtlı Kartla Ödeme) (Priority: P1)

Bir merchant (site), daha önce kasaya kaydedilmiş bir kartla (yalnız opak vault token'ıyla, kart
numarası/güvenlik kodu OLMADAN) belirli bir tutarı ve taksit sayısını gateway'e ileterek ödeme
çeker. Gateway saklı kart kimliklerini çözer, ödeme sağlayıcısına (iyzico) NonSecure çekim isteği
gönderir, sonucu (başarı/başarısızlık + sağlayıcı işlem kimliği + sağlayıcı maliyeti) alır. Başarılı
çekimde gateway ödemeyi kalıcı kaydeder ve "ödeme tamamlandı" olayını yayınlar.

**Why this priority**: Gateway'in var oluş sebebi — para çekmek. Taksit sorgusu ve tüm üst akış buna
bağlı. Tek başına (curl ile) kanıtlanabilir ve değer üretir (charge + kayıt + olay).

**Independent Test**: 032 ile kaydedilmiş bir kartın vault token'ıyla, merchant token'ı (charge
yetkili) kullanılarak çekim isteği gönderilir; sağlayıcıda işlem oluşur, gateway'de Payment kaydı +
sağlayıcı maliyeti bulunur, olay yayınlanır. Güvenlik kodu hiçbir adımda kullanılmaz.

**Acceptance Scenarios**:

1. **Given** kayıtlı bir kart (vault token) + charge-yetkili Aktif merchant token'ı, **When** geçerli tutar + taksit ile çekim istenir, **Then** sağlayıcıda başarılı işlem oluşur, gateway'de Payment kaydı (sağlayıcı işlem kimliği + tutar + taksit + sağlayıcı maliyeti + Başarılı statü) saklanır ve "ödeme tamamlandı" olayı yayınlanır.
2. **Given** sağlayıcının reddettiği bir çekim (yetersiz limit / geçersiz kart durumu), **When** çekim istenir, **Then** Payment kaydı Başarısız statüyle saklanır (ya da hata döner) ve merchant'a anlaşılır hata iletilir; "tamamlandı" olayı yayınlanmaz.
3. **Given** başka merchant'a ait vault token'ı, **When** merchant kendi token'ıyla çekim dener, **Then** erişim reddedilir (kiracı sınırı — token başka kiracının kartını çözmez).
4. **Given** charge yetkisi olmayan token (Aktif olmayan merchant veya charge scope'suz), **When** çekim istenir, **Then** erişim reddedilir (Active-only charge; fail-closed).
5. **Given** güvenlik kodu (CVC), **When** çekim yapılır, **Then** CVC hiçbir yerde kullanılmaz/istenmez (saklı kart kimliğiyle çekim — Model A).

---

### User Story 2 - Taksit Seçenekleri Sorgusu (Priority: P1)

Merchant, ödeme öncesi bir kart ve tutar için mevcut taksit seçeneklerini sorgular. Gateway,
kartın ilk 6 hanesi (BIN) ve tutarı ödeme sağlayıcısına iletir; sağlayıcı, kartın bankasına göre
uygulanabilir taksit sayılarını ve her taksit için müşterinin ödeyeceği toplam tutarı (banka vade
farkı dahil) döndürür. Merchant bu tabloyu son kullanıcıya gösterir; kullanıcı bir taksit seçer.

**Why this priority**: Taksitli çekim, sağlayıcının döndürdüğü taksit-toplam tutarı (PaidPrice)
gerektirir; bu tablo olmadan taksitli ödeme yapılamaz. US1 ile birlikte taksitli akışı tamamlar.

**Independent Test**: Bir BIN + tutar için taksit sorgusu yapılır; sağlayıcının döndürdüğü taksit
seçenekleri (taksit sayısı + toplam tutar) alınır. Sonuç merchant'a döner.

**Acceptance Scenarios**:

1. **Given** kayıtlı kartın BIN'i + sepet tutarı, **When** taksit sorgusu yapılır, **Then** uygulanabilir taksit seçenekleri (her biri için taksit sayısı + müşteri toplam tutarı) döner.
2. **Given** taksit desteklemeyen bir kart/tutar, **When** sorgu yapılır, **Then** yalnız tek çekim (1 taksit) seçeneği döner.
3. **Given** charge-yetkili Aktif merchant token'ı, **When** sorgu yapılır, **Then** yetki geçer (aynı charge düzlemi).

---

### User Story 3 - ECommerce Checkout Uçtan Uca (Priority: P2)

Son kullanıcı ECommerce sitesinde sepetini oluşturur, ödeme adımında kayıtlı kartını seçer, taksit
seçeneklerini görür, birini seçer ve ödemeyi tamamlar. ECommerce arka planı gateway'e taksit
sorgusu + çekim isteği yapar; başarıda sipariş "ödendi" olur. Kullanıcı kart numarası/güvenlik kodu
girmez (kart zaten kayıtlı, çekim saklı kart kimliğiyle).

**Why this priority**: Gerçek kullanıcı akışı; US1+US2 gateway yeteneğini kurduktan sonra anlam
kazanır. ECommerce'in bugün taslak (stub) olan ödeme adımını gerçek gateway çağrısına bağlar.

**Independent Test**: ECommerce checkout'undan kayıtlı kart + taksit seçilip ödeme tamamlanır;
gateway'de Payment kaydı oluşur, ECommerce'te sipariş "ödendi" durumuna geçer.

**Acceptance Scenarios**:

1. **Given** kayıtlı kartı olan giriş yapmış kullanıcı + sepet, **When** ödeme adımında kart + taksit seçip onaylar, **Then** ödeme çekilir, sipariş "ödendi" olur ve gateway'de Payment kaydı bulunur.
2. **Given** sağlayıcı çekimi reddederse, **When** kullanıcı onaylar, **Then** sipariş oluşmaz/ödenmez ve kullanıcıya anlaşılır hata gösterilir.

---

### Edge Cases

- Güvenlik kodu (CVC) hiçbir uçta yok; saklı kart kimliğiyle çekim (Model A).
- Vault token iptal edilmiş (Revoked) karta işaret ediyorsa çekim reddedilir.
- Taksit = 1 (tek çekim): PaidPrice = Price (vade farkı yok).
- Sağlayıcı maliyeti (komisyon) yanıtta gelir ama efektif komisyon (gateway marjı dahil) BU feature'da hesaplanmaz — yalnız sağlayıcı maliyeti kaydedilir + olayla taşınır.
- Sub-merchant split YOK: ödeme facilitator'ın (gateway'in) ana sağlayıcı hesabına düşer; satıcı sanal hesabına bölünmez (Merchant = site, tek-seviye model).
- Aynı çekim isteği tekrar gönderilirse (çift tıklama) — idempotency bu feature kapsamı dışında (ayrı iş); mükerrer çekim riski not edilir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, charge-yetkili merchant'tan kayıtlı-kart çekim isteğini (vault token + tutar + taksit sayısı + taksitli toplam tutar + alıcı/sepet bilgisi) kabul edip ödeme sağlayıcısına NonSecure çekim göndermelidir; kart numarası/güvenlik kodu istekte YER ALMAZ (saklı kart kimliğiyle).
- **FR-002**: Sistem vault token'ı sahibi merchant kaydına çözmeli (kiracı sınırı: başka merchant'ın token'ı çözülmez) ve iptal edilmiş (Revoked) karta işaret eden token'ı reddetmelidir.
- **FR-003**: Başarılı çekimde sistem ödemeyi kalıcı kaydetmelidir: sağlayıcı işlem kimliği, merchant, vault token, tutar, taksitli toplam tutar, taksit sayısı, sağlayıcı maliyeti (oransal + sabit), statü (Başarılı), zaman. Bu kayıt sonraki iptal/iade + denetim temelidir.
- **FR-004**: Başarısız çekimde sistem "tamamlandı" olayı YAYINLAMAZ; merchant'a alan bazlı/anlaşılır hata döner (Payment kaydı Başarısız olarak saklanabilir).
- **FR-005**: Başarılı çekimde sistem "ödeme tamamlandı" olayını (sağlayıcı maliyetini taşıyan) yayınlamalıdır; bu olay ileride komisyon/sipariş tüketicileri için bağlantı noktasıdır.
- **FR-006**: Sistem, bir kart (ilk 6 hane) + tutar için sağlayıcının taksit seçeneklerini (taksit sayısı + müşteri toplam tutarı) döndüren bir sorgu sunmalıdır.
- **FR-007**: Çekim ve taksit-sorgusu uçları YALNIZ charge yetkisi taşıyan Aktif merchant token'ına açıktır; Aktif olmayan merchant veya yetkisiz token reddedilir (fail-closed). Charge yetkisi hiçbir alt-statüde (Provisioning vb.) verilmez.
- **FR-008**: Efektif komisyon (sağlayıcı maliyeti + gateway marjı) bu feature'da HESAPLANMAZ; ödeme kaydı yalnız sağlayıcı maliyetini tutar; efektif komisyon ayrı bir bağlamın (komisyon) sorumluluğudur ve olay üzerinden ileride hesaplanır (bounded context izolasyonu).
- **FR-009**: Ödeme, facilitator'ın ana sağlayıcı hesabına çekilir; satıcı-payı bölünmesi (sub-merchant split) YAPILMAZ (tek-seviye merchant modeli).
- **FR-010**: Sağlayıcı erişim kimlik bilgileri yapılandırmadan (git-dışı) gelir; kaynak koda gömülmez.
- **FR-011**: ECommerce checkout'u, kayıtlı kart + taksit seçimiyle gateway çekim/sorgu uçlarını çağırıp ödemeyi tamamlayabilmeli; başarıda sipariş "ödendi" olmalıdır (US3).

### Key Entities

- **Payment (Ödeme) — YENİ aggregate**: Bir çekimin kaydı. Sağlayıcı işlem kimliği, merchant, vault token, tutar, taksitli toplam tutar, taksit sayısı, sağlayıcı maliyeti (oransal + sabit), statü (Başarılı/Başarısız), zaman. İptal/iade + denetim temeli.
- **StoredCard (MEVCUT — 032)**: Vault token → sağlayıcı kart kimlikleri (cardUserKey/cardToken) çözümü; değişmez (yalnız okunur).
- **Merchant (MEVCUT)**: Kiracı sınırı + charge yetki bağlamı; değişmez.
- **Ödeme Tamamlandı Olayı**: Sağlayıcı maliyetini taşıyan integration event; komisyon/sipariş tüketicileri için bağlantı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kayıtlı kartın vault token'ı + charge-yetkili token ile yapılan çekim, güvenlik kodu HİÇ kullanılmadan sağlayıcıda başarılı işlem üretir ve gateway'de tek Payment kaydı bırakır.
- **SC-002**: Çekim isteklerinde ve gateway kayıtlarında/loglarında açık kart numarası ve güvenlik kodu 0 adettir (yalnız vault token + sağlayıcı kimlikleri kullanılır).
- **SC-003**: Taksit sorgusu, verilen kart + tutar için sağlayıcının döndürdüğü taksit seçeneklerini (en az tek-çekim) döndürür; seçilen taksitin toplam tutarı çekimde kullanılır.
- **SC-004**: Yetkisiz çekim denemelerinin (Aktif olmayan merchant, charge scope'suz token, başka merchant'ın token'ı) %100'ü reddedilir.
- **SC-005**: Başarılı her çekim tam olarak bir "ödeme tamamlandı" olayı yayınlar (başarısız çekim 0 olay).
- **SC-006**: ECommerce checkout'undan kayıtlı kart + taksit seçilerek yapılan ödeme uçtan uca tamamlanır: gateway'de Payment kaydı + ECommerce'te sipariş "ödendi".

## Assumptions

- Ödeme sağlayıcısı iyzico'dur; çekim NonSecure (3DS yok — kullanıcı kararı, öğrenme/ilk sürüm). 3DS ayrı gelecek iş.
- Taksitli toplam tutar (PaidPrice) sağlayıcının taksit sorgusundan gelir; merchant seçilen taksitin toplam tutarını çekime iletir (ya da gateway sorgudan türetir).
- Sağlayıcı maliyeti (oransal + sabit) çekim yanıtından okunur ve ödeme kaydında saklanır; efektif komisyon hesabı ayrı bağlamda (event tüketimi) — bu feature'da tüketici YOK, yalnız olay yayınlanır.
- Sub-merchant split ve payout bu feature dışıdır (tek-seviye merchant; para ana hesaba çekilir, dağıtım ileride).
- İdempotency (çift-tıklama koruması) bu feature dışıdır; ayrı iş (ConversationId eşleme).
- İptal/iade (Cancel/Refund) bu feature dışıdır; Payment kaydı onların temelini kurar (paymentId saklanır).
- Alıcı/sepet bilgisi sağlayıcının zorunlu alanları için sağlanır; gerçek sepet ECommerce'ten gelir, gateway curl testinde temsilî değerler kullanılır.
- Sandbox ortamı; para fiziken akmaz, sonuçlar temsilî.