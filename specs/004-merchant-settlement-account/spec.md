# Feature Specification: Merchant Settlement Hesabı

**Feature Branch**: `004-merchant-settlement-account`

**Created**: 2026-08-01

**Status**: Draft

**Input**: User description: "Settlement/EOD sonrası merchant'a para yatıracağımız hesabı oluşturmak. Bir merchant için ödeme/payout banka hesabı yönetimi: banka referansı, IBAN, hesap sahibi adı, hesap no, açıklama; hesap bazında aktif/pasif durum. Withdrawal/payout işlemleri bu hesabı hedef alır. Aggregate Merchant.Api/Domains altında yeni bir slice; mevcut Merchant aggregate'ine dokunulmaz. Legacy karşılığı BankAccount entity."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant'a settlement hesabı ekle (Priority: P1)

Operatör bir merchant seçer ve o merchant'a para yatırılacak (settlement/payout) banka
hesabını sisteme ekler: hangi banka, IBAN, hesap sahibi adı, hesap numarası ve açıklama.
Kayıt öncesi IBAN'ın biçimi ve seçilen bankanın geçerli bir banka referansı olduğu
doğrulanır. Başarılı kayıtta hesap "aktif" durumla oluşur ve merchant'ın settlement
hesapları arasında görünür.

**Why this priority**: Settlement/EOD sonrası merchant'a para göndermenin ön koşulu geçerli
bir hedef hesabın var olmasıdır. Bu hesap olmadan payout akışı çalışamaz — özelliğin var
olma nedeni budur, dolayısıyla tek başına MVP.

**Independent Test**: Bir merchant ve bir banka referansı verildiğinde, geçerli IBAN'la bir
settlement hesabı oluşturulabildiği ve geçersiz IBAN / var olmayan bankanın reddedildiği
doğrulanarak bağımsız test edilir.

**Acceptance Scenarios**:

1. **Given** var olan bir merchant ve geçerli bir banka referansı, **When** operatör geçerli
   bir TR IBAN, hesap sahibi adı ve hesap no ile hesap ekler, **Then** hesap "aktif" durumla
   oluşturulur ve kimliği (Id) döner.
2. **Given** biçimi bozuk bir IBAN, **When** operatör hesap eklemeyi dener, **Then** işlem
   reddedilir ve IBAN alanı için biçim hatası döner; hiçbir kayıt oluşmaz.
3. **Given** var olmayan bir banka referansı, **When** operatör hesap eklemeyi dener, **Then**
   işlem reddedilir ve banka alanı için "kayıt bulunamadı" hatası döner.
4. **Given** var olmayan bir merchant, **When** operatör o merchant'a hesap eklemeyi dener,
   **Then** işlem reddedilir ve merchant için "kayıt bulunamadı" hatası döner.

---

### User Story 2 - Merchant'ın settlement hesaplarını listele ve tekil görüntüle (Priority: P2)

Operatör bir merchant'ın tüm settlement hesaplarını listeler ve tek bir hesabın ayrıntısını
görüntüler. Liste yalnızca seçilen merchant'a ait hesapları döndürür (tenant sınırı).

**Why this priority**: Ekleme/güncelleme kararları mevcut hesapların görünürlüğünü gerektirir;
ayrıca payout akışının hedef hesabı seçebilmesi için okuma gerekir. P1'e bağımlı ama ondan
sonra gelir.

**Independent Test**: Bir merchant'a birden çok hesap eklendiğinde, listenin yalnızca o
merchant'ın hesaplarını döndürdüğü ve tekil sorgunun doğru hesabı getirdiği doğrulanarak
test edilir.

**Acceptance Scenarios**:

1. **Given** bir merchant'a eklenmiş iki settlement hesabı, **When** operatör o merchant'ın
   hesaplarını listeler, **Then** yalnızca o iki hesap döner; başka merchant'ın hesapları
   listede yer almaz.
2. **Given** var olan bir hesap kimliği, **When** operatör tekil hesabı sorgular, **Then**
   hesabın banka, IBAN, hesap sahibi, hesap no, açıklama ve durum alanları döner.

---

### User Story 3 - Settlement hesabını güncelle ve durumunu yönet (Priority: P3)

Operatör var olan bir settlement hesabının bilgilerini (IBAN, hesap sahibi adı, hesap no,
açıklama, banka referansı) günceller ve hesabı aktif/pasif yapar. Pasif hesap payout için
seçilemez; kayıt silinmez, yalnızca durumu değişir.

**Why this priority**: Banka bilgisi değişebilir veya bir hesap kullanımdan kaldırılabilir.
Doğruluk için gerekli ama ilk teslimde ekleme+listeleme çekirdeği yeterlidir.

**Independent Test**: Var olan bir hesabın IBAN'ı geçerli yeni bir değerle güncellenebildiği,
geçersiz değerin reddedildiği ve pasife alınan hesabın "pasif" döndüğü doğrulanarak test edilir.

**Acceptance Scenarios**:

1. **Given** var olan bir hesap, **When** operatör geçerli yeni bir IBAN ve hesap sahibi adıyla
   günceller, **Then** değişiklikler kalıcı olur ve güncel değerler döner.
2. **Given** var olan aktif bir hesap, **When** operatör hesabı pasife alır, **Then** hesabın
   durumu "pasif" olur; kayıt silinmez.
3. **Given** güncellemede biçimi bozuk bir IBAN, **When** operatör kaydeder, **Then** işlem
   reddedilir ve mevcut değerler korunur.

---

### Edge Cases

- Aynı merchant'a aynı IBAN'ın ikinci kez eklenmesi: aynı IBAN mükerrer eklenmemelidir; ikinci
  ekleme reddedilir (mükerrer IBAN hatası). Farklı merchant'ların aynı IBAN'ı teoride mümkün
  olsa da kapsam dışı tutulur (bkz. Assumptions).
- IBAN'ın ülke kodu TR değilse: sistem yalnız TL/yurtiçi ödeme desteklediğinden TR dışı IBAN
  reddedilir.
- Hesap sahibi adı veya IBAN boş: zorunlu alan hatası döner.
- Pasif bir hesabın payout hedefi olarak seçilmesi: bu spec ekleme/okuma/güncelleme kapsamındadır;
  payout seçim kuralı (pasif hesap seçilemez) ilgili payout/withdrawal feature'ında uygulanır,
  burada yalnız durum modellenir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, var olan bir merchant'a settlement hesabı eklenmesini sağlamalıdır;
  hesap en az banka referansı, IBAN ve hesap sahibi adı içerir.
- **FR-002**: Sistem, IBAN'ı biçim yönünden doğrulamalıdır; yalnız geçerli TR IBAN kabul edilir,
  geçersiz veya TR dışı IBAN reddedilir.
- **FR-003**: Sistem, hesabın işaret ettiği banka referansının geçerli (var olan bir banka)
  olduğunu doğrulamalıdır; var olmayan banka reddedilir.
- **FR-004**: Sistem, hesabın ait olduğu merchant'ın var olduğunu doğrulamalıdır; var olmayan
  merchant reddedilir.
- **FR-005**: Sistem, bir merchant'ın birden çok settlement hesabına sahip olmasına izin
  vermelidir.
- **FR-006**: Sistem, aynı merchant içinde aynı IBAN'ın mükerrer eklenmesini engellemelidir.
- **FR-007**: Sistem, bir settlement hesabı oluşturulduğunda onu "aktif" durumla kaydetmelidir.
- **FR-008**: Sistem, var olan bir settlement hesabının bilgilerinin (IBAN, hesap sahibi adı,
  hesap no, açıklama, banka referansı) güncellenmesini sağlamalıdır; güncellemede de FR-002/003
  doğrulamaları uygulanır.
- **FR-009**: Sistem, bir settlement hesabının aktif/pasif duruma alınmasını sağlamalıdır;
  durum değişimi kaydı silmez (soft durum yönetimi).
- **FR-010**: Sistem, belirli bir merchant'ın settlement hesaplarını listelemelidir; liste
  yalnızca o merchant'a ait hesapları döndürür (tenant sınırı korunur).
- **FR-011**: Sistem, kimliğiyle tekil bir settlement hesabının ayrıntısını döndürmelidir.
- **FR-012**: Beklenen hatalar (bulunamadı, biçim, iş kuralı ihlali) exception ile değil,
  tipli sonuç + alan bazlı mesajla iletilir.
- **FR-013**: Bu feature mevcut Merchant aggregate'ini değiştirmez; settlement hesabı ayrı bir
  slice/aggregate olarak modellenir ve merchant'a kimlik (MerchantId) ile bağlanır.

### Key Entities *(include if feature involves data)*

- **MerchantSettlementAccount**: Bir merchant'a settlement/payout amacıyla para yatırılacak
  banka hesabı. Öznitelikler: bağlı olduğu merchant kimliği (MerchantId), banka referansı
  (banka kodu, 4 hane — kanonik banka katalogundan), IBAN, hesap sahibi adı (AccountOwnerName),
  hesap numarası (AccountNo), açıklama (AccountDescription), durum (Aktif/Pasif) ve denetim
  alanları (kimlik + oluşturma/güncelleme zamanı). Merchant ile ilişki: bir merchant'ın sıfır
  veya daha çok hesabı olur; her hesap tam bir merchant'a aittir. Şube bilgisi tutulmaz — TR
  IBAN banka+şube+hesabı zaten kodlar.
- **Merchant** (referans): Yalnız kimlik olarak kullanılır; bu feature içinde değiştirilmez.
  Varlığı doğrulanır.
- **Bank** (referans): Settlement hesabının işaret ettiği banka. Yalnız varlık doğrulaması için
  kullanılan referans; bu feature içinde değiştirilmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operatör, geçerli bilgilerle bir merchant'a settlement hesabını tek adımda ekleyip
  hesabın kimliğini alabilir.
- **SC-002**: Geçersiz IBAN veya var olmayan banka/merchant ile yapılan her ekleme/güncelleme
  denemesi, hangi alanın neden reddedildiğini belirten bir hata ile sonuçlanır ve hiçbir kısmi
  kayıt bırakmaz.
- **SC-003**: Bir merchant'ın settlement hesapları listelendiğinde sonuç yalnızca o merchant'a
  ait hesapları içerir; başka merchant'ın hiçbir hesabı görünmez (%100 tenant izolasyonu).
- **SC-004**: Bir hesap pasife alındığında listede/tekil sorguda "pasif" olarak görünür ve
  kayıt korunur (silinmez).
- **SC-005**: Aynı merchant'a aynı IBAN ikinci kez eklenemez.

## Assumptions

- **Yalnız TL/yurtiçi**: Anayasa gereği sistem yalnız TL destekler; bu yüzden hesap para birimi
  bir alan olarak modellenmez ve yalnız TR IBAN kabul edilir. Legacy'deki `CurrencyId` ve SWIFT
  alanları kapsam dışıdır (yurtdışı transfer yok).
- **Yetkilendirme ertelenmiştir**: Endpoint'ler bu aşamada korumasızdır (Identity BC ile gelecek);
  ancak tenant filtreleme (merchant bazlı liste) baştan uygulanır.
- **Silme yok**: Hesaplar fiziksel olarak silinmez; kullanımdan kaldırma pasif durumla yapılır.
  Sert silme kapsam dışıdır.
- **Mükerrer IBAN kapsamı**: Mükerrerlik kontrolü aynı merchant içinde uygulanır; farklı
  merchant'ların aynı IBAN'ı tutması bu feature'da engellenmez.
- **Payout/withdrawal akışı ayrıdır**: Bu feature yalnız hesabın CRUD + durum yönetimini kapsar;
  parayı fiilen gönderen withdrawal/settlement akışı ayrı bir feature'dır ve bu hesabı hedef alır.
- **Banka referansı yerel katalogla doğrulanır (BC izolasyonu)**: Banka Commission BC'de zengin
  bir aggregate; Merchant BC'de yalnız 4-hane koddan ibaret bir referanstır. Kod, Merchant BC'nin
  kendi tuttuğu kanonik banka katalogu kopyasına (statik referans veri) karşı bellekte doğrulanır;
  Commission BC'ye runtime çağrı yapılmaz (cross-BC DB erişimi yasak). Bedeli: katalog nadiren
  değişince iki serviste elle senkron. İleride katalog yönetimi büyürse ayrı Reference BC'ye terfi.