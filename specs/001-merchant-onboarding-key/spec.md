# Feature Specification: Merchant Onboarding + API Key + Admin

**Feature Branch**: `feat/microservices-migration`

**Created**: 2026-07-31

**Status**: Draft

**Input**: User description: "E-Ticaret uygulamasına vermek üzere merchant oluşturma ve MerchantKey üretme; merchant için kart kombinasyonu başına komisyon belirleme; bunları yöneten bir admin. Design doc: docs/superpowers/specs/2026-07-31-merchant-onboarding-key-design.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin merchant oluşturur ve API key alır (Priority: P1)

Yetkili bir admin sisteme giriş yapar, yeni bir merchant kaydı oluşturur (isim, iletişim,
adres, MCC, webhook). Kayıt oluşur oluşmaz sistem o merchant'a bağlı bir API key üretir ve
ekranda **bir kez** gösterir. Admin bu key'i tamamlanmış E-Ticaret uygulamasına verir;
e-ticaret bundan sonra ödeme çağrılarında bu key'i kullanır.

**Why this priority**: E-Ticaret uygulaması ödeme yapabilmek için bir merchant kimliği ve
key olmadan çalışamaz. Bu, tüm entegrasyonun ön koşulu ve sistemin ilk çalışır dilimi (MVP).

**Independent Test**: Admin girişi + merchant oluşturma formu doldurulup gönderildiğinde
yeni merchant listede görünür ve tek-seferlik bir key döner; key ikinci kez görüntülenemez.

> **Uygulama dilimi notu**: Bu hikaye iki ayağa bölünür. Mevcut uygulama dilimi
> (`001-merchant-onboarding-key`) yalnız **registry ayağını** (merchant kaydı + doğrulama)
> teslim eder; **API key üretimi/gösterimi Identity dilimine ertelendi** (bkz. Out of Scope +
> Assumptions). Yukarıdaki "key döner" ölçütü key dilimi geldiğinde geçerlidir.

**Acceptance Scenarios**:

1. **Given** yetkili admin giriş yapmış, **When** geçerli merchant bilgileriyle oluşturma
   gönderir, **Then** merchant kaydı oluşur ve ekranda tek-seferlik bir API key gösterilir.
2. **Given** merchant oluşturuldu, **When** admin aynı key'i tekrar görmeye çalışır,
   **Then** ham key bir daha gösterilmez (yalnız key'in var olduğu/oluşturulma tarihi görünür).
3. **Given** eksik/geçersiz merchant bilgisi (ör. geçersiz e-posta, 4 haneli olmayan MCC),
   **When** admin gönderir, **Then** kayıt oluşmaz ve anlaşılır doğrulama mesajı döner.
4. **Given** bir merchant'ın aktif key sayısı üst sınıra ulaştı, **When** admin yeni key ister,
   **Then** sistem reddeder ve sınırı bildirir.

---

### User Story 2 - Admin kart kombinasyonu başına komisyon belirler (Priority: P2)

Admin bir merchant seçer ve kart kombinasyonları (kart markası × kart tipi × işlem bölgesi
× taksit sayısı) için komisyon oranı girer. Her oran, ilgili bankanın o kombinasyondaki oranından yüksek
olmak zorundadır (aksi merchant zararına satış olur). Admin daha sonra oranları güncelleyebilir.

**Why this priority**: Merchant çalışmaya başlayabilir (P1) ama doğru komisyon olmadan gelir
modeli eksiktir. P1'den sonra gelir, ondan bağımsız test edilebilir.

**Independent Test**: Var olan bir merchant için bir kombinasyona oran girildiğinde kayıt
oluşur; banka oranına eşit/altında bir oran girildiğinde reddedilir.

**Acceptance Scenarios**:

1. **Given** var olan merchant ve banka komisyonu tanımlı, **When** admin banka oranından
   yüksek bir merchant oranı girer, **Then** komisyon kaydı oluşur.
2. **Given** aynı bağlam, **When** admin banka oranına eşit veya altında oran girer,
   **Then** sistem reddeder ve kuralı bildirir.
3. **Given** girilmiş komisyonlar var, **When** admin merchant'ı seçer, **Then** o merchant'a
   ait kombinasyon/oran listesi görünür (başka merchant'ınki görünmez).

---

### User Story 3 - Seed admin ile ilk erişim (Priority: P1)

Sistem ilk ayağa kalktığında, önceden tanımlı (seed) bir admin kullanıcısı ve üretilmiş
parolası hazır olur; parola bir kez log/konsola yazılır. Admin bu kimlikle giriş yapıp
merchant ve komisyon işlemlerini yapabilir. Bu admin yalnızca merchant oluşturma ve komisyon
belirleme yetkisine sahiptir.

**Why this priority**: US1/US2 bir admin kimliği olmadan yapılamaz; bu, onların enabler'ı.
Bootstrap olduğu için P1.

**Independent Test**: Temiz sistemde seed admin bilgileriyle giriş yapılabildiği ve merchant
oluşturma ekranına erişilebildiği doğrulanır; yetkisiz bir kimlik aynı ekranı açamaz.

**Acceptance Scenarios**:

1. **Given** sistem ilk kez ayağa kalktı, **When** seed admin bilgileriyle giriş yapılır,
   **Then** admin merchant/komisyon ekranlarına erişir.
2. **Given** merchant yaratma yetkisi olmayan bir kimlik, **When** merchant oluşturma ucunu
   çağırır, **Then** erişim reddedilir.

---

### Edge Cases

- Merchant oluştu ama key üretimi başarısız olursa? → Akış tek işlem gibi ele alınmalı;
  key üretilemezse admin'e net hata döner ve yarım-kayıt bırakılmamalı (telafi/uyarı).
- Aynı merchant için aynı kart kombinasyonuna ikinci kez komisyon girilirse? → Güncelleme mi,
  hata mı olduğu netleşmeli (varsayım: mevcut oran güncellenir).
- Banka komisyonu tanımsız bir kombinasyona merchant oranı girilmek istenirse? → Reddedilir
  (referans banka oranı yoksa invariant değerlendirilemez).
- Admin ham key'i kaydetmeden ekranı kapatırsa? → Key geri alınamaz; yeni key üretmek gerekir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, herhangi bir merchant/komisyon işleminden önce kullanıcının kimliğini
  merkezi kimlik otoritesi üzerinden doğrulaMALIDIR.
- **FR-002**: Yalnızca merchant-yönetim yetkisine sahip kullanıcılar merchant oluşturabilMELİ
  ve komisyon belirleyebilMELİDİR; yetkisiz erişim reddedilMELİDİR.
- **FR-003**: Sistem, merchant oluştururken isim, iletişim (e-posta+telefon), adres
  (ülke+şehir), MCC ve webhook URL bilgilerini almalı ve doğrulaMALIDIR.
- **FR-004**: Merchant oluşur oluşmaz sistem o merchant'a bağlı bir API key üretMELİ ve ham
  değeri yalnızca **bir kez** göstermelidir; ham değer sonradan hiçbir yerde okunamaMALIDIR.
- **FR-005**: Sistem, bir merchant'ın aynı anda aktif tutabileceği key sayısını sınırlaMALIDIR.
- **FR-006**: Admin bir API key'i iptal edebilMELİDİR; iptal sonrası key kabul edilmeMELİDİR.
- **FR-007**: Sistem, bir merchant için kart kombinasyonu (kart markası × kart tipi × işlem
  bölgesi × taksit sayısı) başına komisyon oranı tanımlamaya izin verMELİDİR. Taksit ekseni
  invariant'ın parçasıdır: oran karşılaştırması taksit-taksit yapılır.
- **FR-008**: Bir merchant komisyon oranı, ilgili banka oranından **kesinlikle yüksek**
  olmalıdır; eşit/düşük oran reddedilMELİDİR.
- **FR-009**: Admin var olan bir komisyon oranını güncelleyebilMELİDİR (aynı invariant geçerli).
- **FR-010**: Bir merchant'ın komisyon verisi listelenirken yalnızca o merchant'ın kayıtları
  dönMELİ; farklı merchant verisi karışMAMALIDIR.
- **FR-011**: Sistem ilk kurulumda, merchant/komisyon yetkisine sahip bir seed admin kimliği
  ve üretilmiş parola sağlaMALIDIR; parola bir kez güvenli biçimde bildirilMELİDİR.
- **FR-012**: Merchant kaydının kaynağı (source of truth) merchant-yönetim tarafıdır; kimlik
  tarafındaki kullanıcı/key bu kayda **bağlı** olarak türetilMELİDİR (ters yön yasak).

### Key Entities

- **Merchant**: Ödeme kabul eden iş kaydı. Nitelikler: isim, iletişim, adres, MCC, webhook,
  durum (aktif/pasif/askıda). Sistemdeki merchant kimliğinin kaynağı.
- **API Key (MerchantKey)**: Bir merchant'a bağlı opak makine anahtarı. Ham değer bir kez
  görünür; saklanan yalnız doğrulama özeti. İptal edilebilir, süresizdir (yalnız iptal).
- **Merchant Commission**: Bir merchant + kart kombinasyonu için oran. Banka oranından yüksek
  olma invariant'ı taşır.
- **Card Combination (Kriter)**: Kart markası × kart tipi × işlem bölgesi × taksit sayısı
  dörtlüsü; komisyonun uygulandığı bağlam. Taksit ekseni banka oranı taksitle değiştiği için
  zorunludur (peşin ≪ çok taksit); invariant taksit-taksit eşleşir.
- **Admin (yetkili kullanıcı)**: Merchant/komisyon yönetimi yetkisine sahip, tek bir merchant'a
  bağlı olmayan (global) kullanıcı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, tek bir akışta merchant oluşturup kullanılabilir bir API key'i 2 dakikadan
  kısa sürede elde edebilir.
- **SC-002**: Üretilen ham API key %100 oranında yalnızca bir kez gösterilir; oluşturulduktan
  sonra hiçbir ekran/uç ham değeri geri veremez.
- **SC-003**: Banka oranına eşit veya altındaki her merchant komisyon denemesi %100 reddedilir.
- **SC-004**: Bir merchant'ın komisyon listesinde başka merchant'a ait 0 kayıt görünür (sızıntı yok).
- **SC-005**: Yetkisiz bir kimliğin merchant oluşturma/komisyon uçlarına her erişim denemesi reddedilir.
- **SC-006**: Temiz kurulumdan sonra seed admin ilk denemede giriş yapıp merchant oluşturabilir.

## Assumptions

- **Kimlik otoritesi = Identity.Server (Duende).** Önceki Keycloak entegrasyonu bu iş kapsamında
  sökülür (söküm kapsamı ayrı iş kalemi olabilir).
- **Yetki modeli = scope-tabanlı, rol YOK** (constitution TODO(AUTHZ_MODEL) bu kararla kapanır;
  anayasa amendment ile güncellenecek). Admin ayrımı, merchant-yönetim scope'unu taşımaktan gelir.
- **Multitenant izolasyon = paylaşımlı veritabanı + tenant ayrımı (Marten conjoined).** Bu
  dilimde yalnız mekanizma ve tenant-scoped tiplerin işaretlenmesi; claim→tenant enforcement
  middleware sonraki dilime bırakılır.
- **Admin UI = ayrı bir Razor Pages uygulaması** (BFF), merkezi kimliğe OIDC ile bağlanır.
- **Provisioning senkrondur**: merchant oluşturma → kimlik tarafında kullanıcı+key üretme tek
  akışta olur (event tabanlı değil), çünkü ham key tek-seferlik sırdır.
- Referans banka komisyonları (kombinasyon başına banka oranı) sistemde önceden tanımlı kabul edilir.
- Sistem yalnız TL çalışır (anayasa alan kısıtı); para birimi çok-değerli modellenmez.

## Out of Scope (bu dilim)

- **API key üretimi/hash/gösterimi + seed admin + kimlik doğrulama/yetki** — bu dilimde YOK;
  Identity dilimine ertelendi (US1 key ayağı, US3, FR-001/002/004/005/006/011). Bu dilim yalnız
  Merchant.Api registry + Commission.Api teslim eder; uçlar korumasız.
- Merchant self-service portal ve merchant kullanıcı girişi.
- Ödeme anında API key çözümü (Payment tarafının key→kimlik doğrulaması).
- Tenant enforcement middleware (claim → tenant filtresi otomasyonu).
- Rol modeli.
- Keycloak sökümünün tam kapsamı (gerekirse ayrı spec).