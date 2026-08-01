# Feature Specification: Settlement Hesabı Yönetim Ekranları (Admin)

**Feature Branch**: `005-settlement-account-admin-ui`

**Created**: 2026-08-01

**Status**: Draft

**Input**: User description: "Admin panelinde merchant settlement hesabı yönetim ekranları. Mevcut Admin BFF içine, bir merchant'ın settlement hesaplarını listeleyen/ekleyen/düzenleyen ekranlar; 004 Merchant.Api settlement-accounts API'sini tüketir; MerchantCommissions ekran desenini izler; yetki yok; backend'e dokunulmaz."

## User Scenarios & Testing *(mandatory)*

Aktör: **Gateway admin** (ödeme gateway'ini işleten platform-tarafı yönetici; merchant değil). Bu
Admin paneli iç bir yönetim aracıdır — merchant self-service DEĞİL. Amaç: gateway admin'in herhangi
bir merchant'a payout için para yatırılacak banka hesaplarını gözle görülebilir şekilde yönetmesi —
şu an yalnız API (curl/Scalar) üzerinden mümkün. Aşağıda "operatör" = bu gateway admin.

### User Story 1 - Bir merchant'ın settlement hesaplarını gör (Priority: P1) 🎯 MVP

Operatör bir merchant seçer ve o merchant'a tanımlı tüm settlement hesaplarını bir listede görür:
banka (kod + ad), IBAN, hesap sahibi, durum (Aktif/Pasif). Liste yalnız seçili merchant'ın
hesaplarını gösterir.

**Why this priority**: Görünürlük ilk değerdir — hesapların doğru/eksik olduğunu görmeden ekleme
veya düzeltme yapılamaz. Tek başına teslim edilince operatör API'ye gitmeden hesapları denetleyebilir.

**Independent Test**: 004 API'siyle bir merchant'a iki hesap tanımla; Admin'de o merchant'ı seç →
liste tam iki satırı doğru alanlarla (banka adı dahil) gösterir; başka merchant seçilince onlar görünmez.

**Acceptance Scenarios**:

1. **Given** iki settlement hesabı olan bir merchant, **When** operatör merchant detay sayfasından
   settlement hesapları ekranına gider, **Then** yalnız o merchant'ın iki hesabı tabloda listelenir
   (banka kod+ad, IBAN, sahip, durum).
2. **Given** hiç hesabı olmayan bir merchant, **When** ekran açılır, **Then** "hesap yok" bilgisi ve
   yeni hesap ekleme bağlantısı gösterilir (boş liste hata değildir).
3. **Given** A ve B merchant'larının hesapları, **When** operatör A'yı görüntüler, **Then** B'nin
   hiçbir hesabı listede yer almaz (tenant izolasyonu).

---

### User Story 2 - Yeni settlement hesabı ekle (Priority: P2)

Operatör seçili merchant'a yeni bir settlement hesabı ekler: bankayı bir listeden seçer, IBAN, hesap
sahibi adı, (opsiyonel) hesap no ve açıklama girer. Kaydettiğinde hesap listeye gelir. Geçersiz giriş
(bozuk IBAN, katalog dışı banka, mükerrer IBAN) anlaşılır bir hata mesajıyla reddedilir ve form
kaybolmaz.

**Why this priority**: Ekleme, ekranın asıl üretkenlik değeri; ama görünürlük (US1) olmadan tek
başına anlamı sınırlı olduğundan P2.

**Independent Test**: Ekleme formunu geçerli TR IBAN + katalog bankası ile gönder → hesap listede
belirir; bozuk IBAN ile gönder → hata mesajı görünür, hesap eklenmez, girilen değerler formda kalır.

**Acceptance Scenarios**:

1. **Given** açık ekleme formu, **When** operatör geçerli banka + geçerli TR IBAN + sahip adı girip
   kaydeder, **Then** hesap oluşturulur ve liste ekranında yeni satır olarak görünür.
2. **Given** ekleme formu, **When** bozuk IBAN girilir, **Then** IBAN'ın geçersiz olduğunu belirten
   hata gösterilir ve hesap eklenmez.
3. **Given** ekleme formu, **When** aynı merchant'ta zaten var olan bir IBAN tekrar girilir, **Then**
   mükerrer kayıt hatası gösterilir.
4. **Given** ekleme formu, **When** banka listeden seçilir, **Then** yalnız geçerli katalog bankaları
   seçilebilir (serbest kod girişi yok).

---

### User Story 3 - Hesabı düzenle ve aktif/pasif yap (Priority: P3)

Operatör var olan bir hesabın bilgilerini (banka, IBAN, sahip, hesap no, açıklama) günceller ve
hesabı aktif ya da pasif yapar. Pasif hesap listede "Pasif" görünür ama silinmez. Geçersiz güncelleme
reddedilir; eski değerler korunur.

**Why this priority**: Düzeltme/durum yönetimi tamamlayıcı; ekleme ve görünürlükten sonra gelir.

**Independent Test**: Var olan hesabı yeni geçerli IBAN + sahiple güncelle → liste yeni değerleri
gösterir; pasife al → satır "Pasif" olur ama listede kalır; bozuk IBAN'la güncelle → hata, eski değer korunur.

**Acceptance Scenarios**:

1. **Given** var olan bir hesap, **When** operatör düzenleme formunda geçerli yeni değerlerle kaydeder,
   **Then** liste güncel değerleri gösterir.
2. **Given** aktif bir hesap, **When** operatör pasife alır, **Then** hesap listede "Pasif" görünür ve
   kayıt silinmez.
3. **Given** düzenleme formu, **When** bozuk IBAN girilir, **Then** hata gösterilir ve hesabın eski
   değerleri korunur.

---

### Edge Cases

- Merchant seçilmeden ekran açılırsa: seçim isteyen bilgi gösterilir (liste boş değil, yönlendirici).
- API erişilemezse (Merchant.Api down): kullanıcıya teknik ayrıntı sızdırmayan bir hata mesajı gösterilir.
- Başka merchant'a ait bir accountId elle URL'e yazılırsa: kayıt bulunamadı gibi ele alınır (tenant sızıntısı yok).
- Çok uzun IBAN/isim girişleri: form alanları API doğrulamasına güvenir; UI ek kısıt koymaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admin paneli, bir merchant'ın settlement hesaplarını, o merchant'ın detay sayfasından
  erişilen ayrı bir ekranda listeleyebilMELİDİR.
- **FR-002**: Liste her hesap için banka kodunu, banka adını, IBAN'ı, hesap sahibi adını ve durumu
  (Aktif/Pasif) göstermelidir.
- **FR-003**: Liste yalnız seçili merchant'ın hesaplarını göstermeli; başka merchant'ın hesabı asla
  görünmemelidir (tenant izolasyonu).
- **FR-004**: Boş liste (hesap yok) bir hata değil, bilgilendirici bir boş-durum olarak gösterilmelidir.
- **FR-005**: Operatör yeni settlement hesabı ekleyebilMELİDİR; form banka seçimi (katalogdan), IBAN,
  hesap sahibi adı, hesap no (opsiyonel) ve açıklama (opsiyonel) alanlarını içermelidir.
- **FR-006**: Banka seçimi, geçerli banka katalogundan yapılmalıdır; serbest banka kodu girişine izin
  verilmemelidir.
- **FR-007**: Ekleme/düzenleme başarısız olduğunda (bozuk IBAN, katalog dışı banka, mükerrer IBAN),
  kullanıcıya anlaşılır bir hata gösterilmeli ve girilen değerler kaybolmamalıdır.
- **FR-008**: Operatör var olan bir hesabı düzenleyebilMELİDİR (banka, IBAN, sahip, hesap no, açıklama).
- **FR-009**: Operatör bir hesabı aktif veya pasif yapabilMELİDİR; pasife alınan hesap listede kalır,
  silinmez.
- **FR-010**: Ekranlar, doğrulama ve hata mesajlarını mevcut Admin panelinin mesaj gösterim biçimiyle
  tutarlı sunmalıdır.
- **FR-011**: Tüm işlemler mevcut settlement-accounts API'si üzerinden yapılmalı; bu feature backend
  (Merchant.Api) davranışını veya veri modelini DEĞİŞTİRMEMELİDİR.
- **FR-012**: Ekranlar mevcut merchant komisyon (MerchantCommissions) ekranlarının gezinme ve düzen
  desenini izlemelidir (tutarlı operatör deneyimi).
- **FR-013**: Yetkilendirme bu sürümde kapsam dışıdır (proje genelinde ertelendi); ekranlar korumasızdır.
  Erişim yalnız gateway admin'e açık kabul edilir; bu iç panel merchant'lara sunulmaz (merchant
  self-service değil). Rol modeli netleşince (TODO AUTHZ_MODEL) gateway-admin yetkisiyle korunur.

### Key Entities *(include if feature involves data)*

- **Settlement Hesabı (görünüm)**: Bir merchant'a ait banka hesabının operatöre gösterilen temsili —
  banka (kod+ad), IBAN, hesap sahibi, hesap no, açıklama, durum, oluşturma zamanı. Veri kaynağı 004
  API'sidir; bu feature yeni veri saklamaz.
- **Banka (katalog girişi)**: Seçilebilir banka listesi — kod + ad. Ekleme/düzenleme formunda seçim
  kaynağı.
- **Merchant (bağlam)**: Hesapların bağlı olduğu merchant; ekranların tenant sınırını belirler. Mevcut
  merchant listesinden/detayından gelir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operatör, bir merchant'ın tüm settlement hesaplarını API aracı (curl/Scalar) kullanmadan,
  yalnız Admin panelinden görebilir.
- **SC-002**: Operatör yeni bir geçerli settlement hesabını tek bir form ekranında ekleyip sonucu
  listede anında görebilir.
- **SC-003**: Geçersiz girişlerin (bozuk IBAN, katalog dışı banka, mükerrer IBAN) tamamı kullanıcıya
  anlaşılır bir mesajla reddedilir ve hiçbir hatalı/kısmi kayıt oluşmaz.
- **SC-004**: Bir merchant'ın ekranında başka hiçbir merchant'ın hesabı hiçbir koşulda görünmez.
- **SC-005**: Operatör bir hesabı pasife aldığında hesap listede "Pasif" olarak kalır; hiçbir hesap
  ekranlar üzerinden kalıcı silinemez.

## Assumptions

- Ekranlar mevcut Admin BFF içine eklenir; ayrı bir uygulama/arayüz teknolojisi getirilmez.
- Banka katalogu, ekleme formundaki seçim için erişilebilir kabul edilir (mevcut statik katalog
  yeterli); yeni bir katalog servisi bu feature kapsamında değildir.
- Tüm veri ve doğrulama 004 Merchant.Api settlement-accounts API'sinden gelir; UI ek iş kuralı koymaz,
  yalnız API sonucunu sunar.
- Yetki/oturum yok (proje genelinde ertelendi); ekran erişimi korumasızdır ve bu bilinçlidir.
- Operatör mevcut merchant kayıtlarına (001) ve onların Id'lerine erişebilir; settlement ekranları
  merchant bağlamından türetilir.
- Mobil/uyarlanır tasarım hedefi yok; mevcut Admin panelinin masaüstü düzeni korunur.