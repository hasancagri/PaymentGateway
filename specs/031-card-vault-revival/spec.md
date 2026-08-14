# Feature Specification: Kart Vault Dirilişi

**Feature Branch**: `031-card-vault-revival`

**Created**: 2026-08-14

**Status**: Draft

**Input**: User description: "Kart vault dirilişi (Model B — kendi vault'umuz): 022'de sökülen 017 kart saklama düzlemi, ECommerce'in CANLI tokenizer sözleşmesine birebir hizalanarak Payment.Api'de yeniden kurulur. StoredCard aggregate; kart numarası yalnız korumalı saklanır, güvenlik kodu gateway'e hiç gelmez, yanıt yalnız opak token; soft revoke. Sağlayıcıya (iyzico) çağrı YOK — 'banka tarafı ileride bize' kararıyla tutarlı. ECommerce tarafına sıfır dokunuş."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kart Kaydetme (Tokenize) (Priority: P1)

ECommerce sitesinin son kullanıcısı, profilindeki kart formuna kart numarasını, son kullanma
tarihini ve kart sahibi adını girer. ECommerce, kartı kendi merchant kimliğiyle gateway'in kart
kasasına iletir (güvenlik kodu CVV gateway'e HİÇ gönderilmez). Gateway kartı doğrular (numara
sağlaması, son kullanma gelecekte), kart numarasını yalnız korumalı biçimde saklar ve karşılığında
tek bir **opak token** döndürür. ECommerce token'ı ve kendisinin yerelde türettiği gösterim
bilgilerini (marka, son 4 hane) kendi cüzdanında saklar; kullanıcı listede kartını görür.

**Why this priority**: Kasanın varlık sebebi — token doğmadan silme de ödeme de yok. ECommerce'in
kart ekleme akışı bugün bu ucu çağırıp hata aldığı için kırık; bu story onu canlandırır.

**Independent Test**: Merchant token'ıyla kasaya geçerli kart gönderilir; yanıtta yalnız opak
token döner; gateway kayıtlarında kart numarası açık görünmez. ECommerce profil ekranından uçtan
uca: kart ekle → listede marka+son4.

**Acceptance Scenarios**:

1. **Given** Aktif bir merchant'ın makine token'ı, **When** geçerli kart (numara + son kullanma + sahip adı) kasaya gönderilir, **Then** yanıt yalnız opak token içerir; kart numarası veya başka kart verisi yanıtta yer almaz.
2. **Given** numara sağlaması tutmayan bir kart numarası, **When** gönderilir, **Then** kayıt oluşmaz ve alan hatası döner.
3. **Given** son kullanma tarihi geçmiş bir kart, **When** gönderilir, **Then** kayıt oluşmaz ve alan hatası döner.
4. **Given** aynı kart ikinci kez gönderilir, **When** kayıt olur, **Then** YENİ ve farklı bir token üretilir (idempotensi yok — bilinçli 017 kararı; mükerrer kayıt merchant'ın sorumluluğu).
5. **Given** başka bir merchant'ın kimliğiyle adreslenen kasa ucu, **When** merchant kendi token'ıyla çağırır, **Then** erişim reddedilir (kiracı sınırı).

---

### User Story 2 - Kart Silme (Revoke) (Priority: P2)

Son kullanıcı ECommerce profilinden kayıtlı kartını siler. ECommerce önce kendi cüzdan kaydını
düşürür, ardından gateway kasasındaki token'ı iptal eder (mevcut davranış: gateway'e ulaşamazsa
silme yine tamamlanır — iptal en-iyi-çaba). Gateway'de kart kaydı iptal edilmiş olarak işaretlenir
ve tarihçede kalır (fiziksel silinmez).

**Why this priority**: Yaşam döngüsünün kapanışı; kayıt olmadan anlamı yok.

**Independent Test**: Var olan token iptal edilir → kayıt iptal durumuna geçer; aynı token ikinci
kez iptal edilirse işlem yine başarılı kabul edilir (idempotent silme); bilinmeyen token'da kayıt
bulunamadı döner.

**Acceptance Scenarios**:

1. **Given** kasada aktif bir kart token'ı, **When** sahibi merchant iptal çağrısı yapar, **Then** kayıt iptal (Revoked) durumuna geçer ve tarihçede kalır.
2. **Given** iptal edilmiş bir token, **When** tekrar iptal edilir, **Then** işlem başarılı kabul edilir (idempotent — çift tıklama/yeniden deneme kırılmaz).
3. **Given** var olmayan bir token, **When** iptal edilir, **Then** kayıt bulunamadı yanıtı döner.
4. **Given** başka merchant'a ait bir token, **When** merchant kendi kimliğiyle iptal etmeye çalışır, **Then** erişim reddedilir (kiracı sınırı — token başka kiracının kaydını çözmez).

---

### Edge Cases

- Güvenlik kodu (CVV) sözleşmede hiç yok — gönderilse bile kabul eden alan bulunmaz (kasaya asla girmez).
- Kart numarası boşluk/tire ile gelirse normalize edilir; sağlama normalize edilmiş haliyle yapılır.
- 12 haneden kısa / 19 haneden uzun numaralar reddedilir.
- Pasif/Askıda merchant zaten kasa yetkisi taşıyan token alamaz (mevcut statü-kapılı yetki — bu özellik değiştirmez, kullanır).
- Gateway kayıtları (log dahil) hiçbir yerde açık kart numarası içermez; saklanan tek kopya korumalı biçimdir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, yetkili merchant'tan kart kaydetme isteğini (kart numarası + son kullanma + kart sahibi adı) kabul etmeli ve karşılığında YALNIZ opak, tahmin edilemez bir token döndürmelidir; yanıt başka hiçbir kart verisi taşımaz.
- **FR-002**: Güvenlik kodu (CVV) sözleşmenin parçası DEĞİLDİR; sistem bu veriyi hiçbir biçimde kabul etmez ve saklamaz.
- **FR-003**: Sistem kart numarasını kayıt öncesi doğrulamalıdır: yalnız rakam, 12-19 hane, sağlama (Luhn) geçerli; son kullanma `AA/yy` biçiminde ve gelecekte. İhlalde kayıt oluşmaz, alan hatası döner.
- **FR-004**: Kart numarası sistemde YALNIZ korumalı (şifrelenmiş) biçimde durur; hiçbir sorgu/yanıt/log açık numarayı içermez. Gösterim/denetim için ilk 6 hane (BIN), son 4 hane ve marka kayıt anında türetilip ayrıca saklanır.
- **FR-005**: Her kayıt, kaydı yapan merchant'a bağlıdır (kiracı sınırı): merchant yalnız kendi kimliğiyle adreslenen kasa uçlarına erişir; başka merchant'ın token'ı üzerinde işlem yapamaz.
- **FR-006**: Aynı kartın tekrar kaydı yeni bağımsız token üretir (idempotensi bilinçli olarak yok — 017 kararı korunur).
- **FR-007**: Merchant, token ile kaydı iptal edebilmelidir: kayıt iptal durumuna geçer, tarihçede kalır (fiziksel silinmez); iptal edilmiş kaydın tekrar iptali başarılı kabul edilir (idempotent); bilinmeyen token kayıt-bulunamadı döner.
- **FR-008**: Kasa uçları mevcut kart-kasası yetki düzlemini kullanır (kart yazma yetkisi yalnız Aktif merchant'ta — mevcut statü-kapılı zincir değiştirilmez).
- **FR-009**: Dış sözleşme, ECommerce'in CANLI tokenizer istemcisiyle birebir uyumludur; ECommerce tarafında hiçbir kod/config değişikliği gerekmez (sıfır dokunuş — başarı ölçütü SC-001 bunu kanıtlar).
- **FR-010**: Sağlayıcıya (iyzico) hiçbir çağrı yapılmaz; kasa tamamen gateway içindedir (Model B kararı).

### Key Entities

- **StoredCard (Kayıtlı Kart)**: Kasa kaydı — opak token (kimlik), sahibi merchant, korumalı kart numarası, türetilmiş BIN/son4/marka, son kullanma, kart sahibi adı, durum (Aktif/İptal). Tarihçe silinmez.
- **Merchant**: Mevcut aggregate — yalnız kiracı sınırı referansı; değişmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: ECommerce tarafında SIFIR değişiklikle, son kullanıcı profil ekranından kart ekleme akışı uçtan uca tamamlanır: kart ekle → listede marka+son4 görünür (bugün bu akış gateway ucu olmadığı için kırıktır).
- **SC-002**: Kart kaydetme yanıtlarının %100'ü yalnız opak token taşır; gateway veri deposunda ve loglarında açık kart numarası 0 adettir.
- **SC-003**: Geçersiz kartların (sağlama/son kullanma/eksik alan) %100'ü kayıt üretmeden alan bazlı hatayla döner.
- **SC-004**: Silme akışı uçtan uca: ECommerce listeden sil → gateway kaydı İptal durumuna geçer; tekrar silme denemesi hata üretmez.
- **SC-005**: Kiracı sınırı ihlal denemelerinin (başka merchant'ın kasasına/token'ına erişim) %100'ü reddedilir.

## Assumptions

- Kart numarası koruması geliştirme-dönemi yöntemiyle yapılır (simetrik şifreleme, sabit dev anahtarı — 017'nin kanıtlanmış deseni); üretimde anahtar yönetimi (KMS/HSM) ayrı iştir ve kapsam dışıdır.
- Marka tespiti BC içinde basit önek kuralıyla yapılır (Visa/Mastercard/Amex/Troy + Bilinmeyen); eski paylaşılan kart taksonomisi (021'de silindi) geri getirilmez.
- Eski sözleşmedeki kart güncelleme ucu geri gelmez (ECommerce çağırmıyor — kart düzenleme ekranı yok; YAGNI).
- Kayıtlı kartla ÖDEME bu kapsamda yok — ödeme akışı ayrı spec (kasa yalnız sakla/iptal döngüsünü kurar).
- Kart listeleme/okuma ucu gateway'de yok — gösterim verisini ECommerce kendi cüzdanında tutuyor (canlı davranış); gateway'den kart okumak gerekmiyor.
- 022 öncesi kasa verisi yok; temiz başlangıç.
