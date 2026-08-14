# Feature Specification: Agent-Bazlı Merchant Onboarding Dirilişi

**Feature Branch**: `029-agent-merchant-onboarding`

**Created**: 2026-08-14

**Status**: Draft

**Input**: User description: "Agent-bazlı merchant onboarding dirilişi: ECommerce ChatAgent admin personası, DropShop Merchant.Api /mcp yüzeyindeki submit_registration + registration_status tool'larıyla merchant kayıt başvurusu açar ve durumunu sorgular. Gateway tarafında RegisterRequest aggregate'i (023 Merchant alan seti) başvuruyu taşır. Admin, Admin UI'daki RegisterRequests ekranından karar verir; onayda merchant Active doğar. registration_status e-posta parametresiyle sorgulanır; Approved yanıtı MerchantId + MerchantKey'i döndürür (dev-açık karar). ECommerce tarafında yalnız config + prompt alan enjeksiyonu güncellenir; tool adları ve akış korunur."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Metinle Kayıt Başvurusu (Priority: P1)

ECommerce sitesinin yöneticisi, kendi yönetim panelindeki sohbet ekranına "gateway'e kayıt olmak istiyorum" yazar. Sohbet asistanı şirket bilgilerini (işyeri tipi, unvan/isim, e-posta, telefon, adres, IBAN, yetkili ad-soyad ve tipe göre TCKN / vergi bilgileri) toplayıp gateway'e başvuru olarak iletir. Başvuru gateway tarafında "Beklemede" durumuyla kaydolur ve yöneticiye başvurunun alındığı metinle bildirilir.

**Why this priority**: Akışın giriş kapısı — başvuru düşmeden onay da durum sorgusu da yok. Tek başına bile değer üretir (talep havuzu oluşur).

**Independent Test**: ECommerce sohbetinden (veya doğrudan MCP aracıyla) geçerli bir başvuru gönderilir; gateway tarafında Beklemede durumda tek kayıt oluştuğu ve sohbete alındı onayı döndüğü görülür.

**Acceptance Scenarios**:

1. **Given** geçerli şirket bilgileri, **When** yönetici sohbet üzerinden başvuruyu gönderir, **Then** başvuru Beklemede durumuyla kaydolur ve sohbette başvurunun alındığı bilgisi görünür.
2. **Given** işyeri tipiyle uyumsuz alan seti (ör. şahıs tipi ama TCKN yok), **When** başvuru gönderilir, **Then** başvuru kaydolmaz ve hangi alanın eksik/uyumsuz olduğu metinle bildirilir.
3. **Given** geçersiz IBAN veya e-posta, **When** başvuru gönderilir, **Then** başvuru kaydolmaz ve ilgili alan hatası bildirilir.
4. **Given** aynı e-posta ile Beklemede bir başvuru zaten varken, **When** yeni başvuru gönderilir, **Then** yeni kayıt açılmaz ve bekleyen başvurunun sürdüğü bildirilir.

---

### User Story 2 - Admin Onay/Red Kararı (Priority: P1)

Gateway yöneticisi, Admin panelindeki "Merchant Talepleri" ekranında bekleyen başvuruları görür. Başvuruyu inceler (gerekirse iletişim bilgisi üzerinden aday ile dışarıda görüşür); Onayla derse merchant kaydı oluşur ve aktif hâle gelir, Reddet derse neden girerek başvuruyu kapatır. Karar verilen başvurular listede tarihçe olarak durumuyla görünmeye devam eder.

**Why this priority**: Başvurunun değere dönüştüğü nokta — onay olmadan merchant doğmaz. US1 ile birlikte uçtan uca minimum akışı tamamlar.

**Independent Test**: Beklemede bir başvuru elle oluşturulup Admin ekranından Onayla/Reddet ile karar verilir; onayda merchant'ın oluştuğu ve makine erişiminin açıldığı, redde nedenin kaydedildiği görülür.

**Acceptance Scenarios**:

1. **Given** Beklemede bir başvuru, **When** yönetici Onayla der, **Then** başvurudaki bilgilerle merchant oluşur, aktif duruma geçer ve başvuru Onaylandı durumuna bağlanmış merchant kimliğiyle güncellenir.
2. **Given** Beklemede bir başvuru, **When** yönetici neden girerek Reddet der, **Then** başvuru Reddedildi durumuna geçer ve neden kayıtta saklanır.
3. **Given** onaylanmış bir merchant, **When** merchant sistemdeki kimlik servisinden makine erişimi talep eder, **Then** erişim açılmıştır (onay, erişim tarafını otomatik besler).
4. **Given** karar verilmiş (Onaylandı/Reddedildi) bir başvuru, **When** yönetici tekrar karar vermeye çalışır, **Then** işlem reddedilir ve mevcut durum korunur.

---

### User Story 3 - Metinle Durum Sorgusu ve Kimlik Teslimi (Priority: P2)

ECommerce yöneticisi sohbete "başvurum ne durumda?" yazar. Asistan, başvurudaki e-posta ile gateway'den durumu sorgular. Beklemedeyse bekleme bilgisi, reddedildiyse neden döner; onaylandıysa yanıt merchant kimliği (MerchantId) ve erişim anahtarını (MerchantKey) içerir. Yönetici bu ikiliyi ECommerce'in mevcut kimlik-bilgisi formuna girerek entegrasyonu tamamlar.

**Why this priority**: Sonucun karşı tarafa ulaşma yolu; onay gerçekleşmeden değeri yok, bu yüzden P1'lerin ardından gelir. Anahtar teslim sorununu da (şimdilik bilinçli-açık modelle) çözer.

**Independent Test**: Onaylanmış bir başvurunun e-postasıyla durum sorgusu yapılır; yanıtta merchant kimliği + erişim anahtarının döndüğü ve bu bilgilerle makine erişimi alınabildiği görülür.

**Acceptance Scenarios**:

1. **Given** Beklemede başvuru, **When** e-posta ile durum sorulur, **Then** beklemede olduğu bilgisi döner.
2. **Given** Reddedilmiş başvuru, **When** durum sorulur, **Then** reddedildiği ve nedeni döner.
3. **Given** Onaylanmış başvuru, **When** durum sorulur, **Then** onaylandığı bilgisiyle birlikte MerchantId + MerchantKey döner.
4. **Given** hiç başvurusu olmayan bir e-posta, **When** durum sorulur, **Then** kayıt bulunamadığı bilgisi döner.
5. **Given** aynı e-posta ile birden çok geçmiş başvuru, **When** durum sorulur, **Then** en son başvurunun durumu döner.

---

### Edge Cases

- Reddedilen aday aynı e-posta ile yeniden başvurursa yeni başvuru kabul edilir (red nihai engel değildir); tarihçe iki kaydı da korur.
- Onaylanmış başvurunun e-postasıyla yeniden başvuru gönderilirse yeni kayıt açılmaz; zaten onaylı olduğu bildirilir.
- Onay anında başvurudaki bilgiler merchant doğrulamasından geçemezse (teorik — başvuru zaten aynı kurallarla doğrulanır) onay işlemi hatayla durur, başvuru Beklemede kalır.
- Sohbet asistanının gateway bağlantısı yoksa/başarısızsa yönetici metinle "şu an ulaşılamıyor" bilgisi alır; başvuru sessizce kaybolmaz.
- Durum sorgusunda büyük/küçük harf farklı yazılmış e-posta aynı kaydı bulur.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, yetkili makine istemcisinden gelen kayıt başvurusunu (işyeri tipi, isim, e-posta, telefon, adres, IBAN, yetkili ad-soyad ve tipe göre koşullu TCKN/vergi dairesi/vergi no/ticari unvan alanları) kabul edip Beklemede durumuyla kalıcı kaydetmelidir.
- **FR-002**: Sistem, başvuru alanlarını merchant kaydıyla birebir aynı kurallarla doğrulamalıdır: işyeri tipi–alan uyum matrisi, IBAN geçerliliği, e-posta biçimi. Doğrulama hatası başvuru kaydı üretmez ve alan bazında hata bildirir.
- **FR-003**: Sistem, aynı e-posta ile Beklemede başvuru varken yeni başvuruyu reddetmeli; Onaylanmış başvuru e-postasıyla gelen yeni başvuruyu "zaten onaylı" bilgisiyle reddetmeli; Reddedilmiş e-postadan yeni başvuruya izin vermelidir.
- **FR-004**: Gateway yöneticisi tüm başvuruları (durum ve başvuru zamanı dahil) yönetim ekranında listeleyebilmelidir; karar verilmiş başvurular tarihçe olarak görünür kalır.
- **FR-005**: Yönetici Beklemede başvuruyu onaylayabilmelidir; onayda başvuru bilgileriyle merchant oluşur ve aktif duruma geçer, başvuru Onaylandı durumuna ve oluşan merchant kimliğine bağlanır. Merchant oluşumu, mevcut kimlik-senkron mekanizmasını (makine erişiminin açılması) otomatik tetikler.
- **FR-006**: Yönetici Beklemede başvuruyu neden girerek reddedebilmelidir; neden kayıtta saklanır ve durum sorgusunda karşı tarafa iletilir.
- **FR-007**: Karar verilmiş (Onaylandı/Reddedildi) başvuru üzerinde ikinci bir karar işlemi reddedilir.
- **FR-008**: Sistem, e-posta parametresiyle durum sorgusunu yanıtlamalıdır: aynı e-postanın en son başvurusu esas alınır; Beklemede/Reddedildi (nedenle)/Onaylandı ayrımı döner; e-posta eşleşmesi büyük/küçük harf duyarsızdır.
- **FR-009**: Onaylanmış başvurunun durum yanıtı MerchantId ve MerchantKey'i içermelidir (bilinçli dev-açık karar; ileride güvenli teslim modeliyle değişecek).
- **FR-010**: Başvuru ve durum sorgusu yalnız sohbet-asistanı yüzeyinden (agent araçları `submit_registration` ve `registration_status`; mevcut araç adları korunur) sunulur; bu yüzey yalnız yetkili makine istemcilerine açıktır ve ECommerce tarafındaki mevcut asistan akışı araç sözleşmesi değişmeden çalışmaya devam eder.
- **FR-011**: ECommerce tarafında başvuru alan seti yeni sözleşmeye göre güncellenir (yapılandırma + asistan yönergesi); asistan eksik alanları yöneticiden metinle toplar, uydurma değer üretmez.
- **FR-012**: Gateway'e yeni bir yetkili makine istemcisi (ECommerce onboarding istemcisi) tanımlanır; sırrı yapılandırmadan gelir, kod içinde tutulmaz.

### Key Entities

- **RegisterRequest (Kayıt Başvurusu)**: Merchant adayının başvurusu. Merchant alan setinin tamamı + durum (Beklemede/Onaylandı/Reddedildi) + red nedeni + onayda bağlanan merchant kimliği + başvuru zamanı. Tarihçe silinmez.
- **Merchant**: Mevcut 023 aggregate'i — onayda başvuru bilgilerinden doğar; bu özellik alanlarını DEĞİŞTİRMEZ, yalnız yeni doğum yolu ekler.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: ECommerce yöneticisi, panel sohbetinden başvuruyu tek oturumda (form ekranına hiç girmeden) tamamlayabilir; başvurunun alındığı bilgisi aynı sohbette görünür.
- **SC-002**: Gateway yöneticisi bekleyen başvuruyu ekranda görüp tek işlemle onaylar; onaydan hemen sonra merchant listede Aktif görünür ve merchant makine erişimi alabilir.
- **SC-003**: Onaylanan adayın kimlik bilgileri (MerchantId + MerchantKey) sohbet üzerinden alınıp ECommerce'in mevcut kimlik formuna girildiğinde uçtan uca entegrasyon insan-eliyle dosya/DB müdahalesi olmadan tamamlanır.
- **SC-004**: Geçersiz/eksik alanlı başvuruların %100'ü kayıt üretmeden, alan bazında anlaşılır hata mesajıyla geri döner.
- **SC-005**: Aynı e-posta ile mükerrer Beklemede başvuru oluşturulamaz (0 mükerrer kayıt).

## Assumptions

- Aday ile pazarlık/iletişim (telefon, e-posta) sistem dışında yürür; sistem yalnız başvuru–karar–sonuç zincirini taşır.
- Onaylanan merchant doğrudan Aktif doğar (023 davranışı); ayrı bir "hazırlık" ara durumu bu kapsamda yok.
- MerchantId + MerchantKey'in sohbet yanıtında açık dönmesi bilinçli geliştirme-dönemi kararıdır; güvenli teslim (tek-kullanımlık bağlantı/redeem) ayrı bir gelecek iştir ve bu spec'in kapsamı dışındadır.
- ECommerce tarafındaki sohbet asistanı, araç adları ve çağrı sözleşmesi korunduğu için yalnız alan seti güncellemesiyle çalışmaya devam eder; ECommerce'de yeni ekran yapılmaz (mevcut sohbet + mevcut kimlik-bilgisi formu yeter).
- Başvuru hacmi düşüktür (gün başına onlu mertebede); listeleme sayfalama gerektirmez.
- Eski (022 öncesi) başvuru verisi yoktur; temiz başlangıç varsayılır.
