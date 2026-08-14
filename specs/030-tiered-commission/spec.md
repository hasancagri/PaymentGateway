# Feature Specification: Tutar-Kademeli Komisyon Marjı

**Feature Branch**: `030-tiered-commission`

**Created**: 2026-08-14

**Status**: Draft

**Input**: User description: "kademeli komisyon — gateway marjı işlem tutarına göre farklı yüzde/sabit ücretle uygulanabilsin (ör. 0–1.000 TL: %2,5+1; 1.000–10.000: %2+1; 10.000+: %1,8+0). Kullanıcı kararı 2026-08-14: 'Ben tutara göre komisyon yüzdelerinin değişmesi taraftarıyım.'"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kademeli Tarife Tanımlama (Priority: P1)

Gateway yöneticisi, Komisyon Politikaları ekranında bir merchant için tek satırlık marj yerine
tutar aralıklarına bölünmüş bir tarife tablosu girer: her kademe "alt sınırdan itibaren" bir
aralığı, bir oranı ve bir sabit ücreti taşır (ilk kademe 0'dan başlar, son kademe açık uçludur).
Sistem tabloyu bütün olarak doğrular: aralıklar 0'dan başlayıp boşluksuz/bindirmesiz dizilmeli,
her kademenin oranı ve sabit ücreti mevcut tavanların içinde kalmalıdır.

**Why this priority**: Özelliğin kendisi — tarife girilemeden kademeli hesap da yok. Tek kademeli
tablo bugünkü davranışın birebir karşılığı olduğundan geriye uyum da bu story'de doğar.

**Independent Test**: Ekrandan (veya API'den) 3 kademeli tarife girilir; kayıt listede kademeleriyle
görünür. Bozuk tablolar (boşluklu/bindirmeli aralık, 0'dan başlamayan ilk kademe, tavan aşımı,
boş tablo) alan bazlı hatayla reddedilir.

**Acceptance Scenarios**:

1. **Given** politikası olmayan Active bir merchant, **When** yönetici 0–1.000 (%2,5+1), 1.000–10.000 (%2+1), 10.000+ (%1,8+0) tablosuyla politika oluşturur, **Then** politika üç kademesiyle kaydolur ve listede görünür.
2. **Given** tarife girişi, **When** ilk kademe 0'dan başlamıyorsa veya aralıklar arasında boşluk/bindirme varsa, **Then** kayıt reddedilir ve hangi kademenin sorunlu olduğu bildirilir.
3. **Given** tarife girişi, **When** herhangi bir kademenin oranı 0,20'yi veya sabit ücreti 100 TL'yi aşarsa, **Then** kayıt reddedilir.
4. **Given** tek kademeli (0'dan açık uca) tablo, **When** kaydedilir, **Then** bugünkü tek-oranlı davranışla birebir aynı sonucu üretir.
5. **Given** merchant'ın Active politikası varken, **When** ikinci politika oluşturulmak istenirse, **Then** mevcut tekil-aktif kuralı aynen çalışır (reddedilir).

---

### User Story 2 - Tutara Göre Doğru Kademeden Hesap (Priority: P1)

Efektif komisyon hesabı, işlem tutarının düştüğü kademenin oranını ve sabit ücretini TÜM tutara
uygular (dilimli/artan oranlı DEĞİL — tek kademe seçilir): marj = tutar × kademe oranı + kademe
sabit ücreti. iyzico maliyeti girdisiyle birleşip efektif komisyonu ve merchant net hakedişini
verir (mevcut hesap akışı; yalnız marj tarafı kademeden gelir).

**Why this priority**: Tarifenin para ürettiği yer; US1 ile birlikte özelliğin çekirdeği.

**Independent Test**: Aynı politikada üç farklı tutarla (kademe içi, kademe sınırında, açık uçlu
son kademede) hesap çağrısı yapılır; her tutar kendi kademesinin oran+sabitiyle hesaplanır.

**Acceptance Scenarios**:

1. **Given** US1'deki üç kademeli tarife, **When** 500 TL için hesap istenir, **Then** marj = 500 × 0,025 + 1 = 13,50 TL.
2. **Given** aynı tarife, **When** 20.000 TL için hesap istenir, **Then** marj = 20.000 × 0,018 + 0 = 360,00 TL.
3. **Given** aynı tarife, **When** tam kademe sınırı olan 1.000 TL için hesap istenir, **Then** 1.000 TL üst kademeye (1.000–10.000) düşer ve marj = 1.000 × 0,02 + 1 = 21,00 TL (sınır değeri üst kademenin alt sınırıdır).
4. **Given** efektif komisyon (iyzico maliyeti + kademe marjı) işlem tutarını aşarsa, **When** hesap istenir, **Then** mevcut koruma aynen çalışır (işlem reddi/hata).

---

### User Story 3 - Tarife Güncelleme (Priority: P2)

Yönetici mevcut politikanın tarifesini yeni bir kademe tablosuyla değiştirir (marj güncelleme
bugün nasıl çalışıyorsa aynı kapıdan — tablo bütün olarak verilir, tek kademe düzenlenmez).
Eski tarifeyle karşılaştırma/tarihçe ihtiyacı olan yönetici, bugünkü yolla ilerler: mevcut
politikayı pasifleştirip yenisini oluşturur — pasif kayıt eski tarifesiyle listede kalır.

**Why this priority**: İşletim ihtiyacı; çekirdek hesap çalıştıktan sonra anlamlı.

**Independent Test**: Politikanın tablosu yeni kademelerle güncellenir; sonraki hesaplar yeni
tarifeden döner. Pasifleştir+yeni-oluştur yolunda eski kayıt kademeleriyle görünür kalır.

**Acceptance Scenarios**:

1. **Given** üç kademeli Active politika, **When** yönetici iki kademeli yeni tabloyla günceller, **Then** politika yeni tablosuyla kaydolur ve sonraki hesaplar yeni tarifeden yapılır.
2. **Given** güncelleme tablosu bozuksa (US1 kuralları), **When** gönderilir, **Then** reddedilir ve mevcut tarife değişmeden kalır.

---

### Edge Cases

- Tek kademeli tablo = bugünkü düz model; sistemde "kademesiz" ayrı bir mod YOK (iç temsil hep tablo).
- 0 TL / negatif tutarla hesap istenirse mevcut tutar doğrulaması reddeder (kademe seçimine gelmez).
- Çok büyük tutar her zaman açık uçlu son kademeye düşer (son kademenin üst sınırı yoktur — tanım gereği boşluk oluşamaz).
- Kademe sayısı üst sınırı aşarsa (bkz. Assumptions) tablo reddedilir.
- Mevcut (029 öncesi girilen) tek-oranlı politika kayıtları dev verisidir; dönüştürme yapılmaz, sıfırdan girilir (bkz. Assumptions).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Politika marjı, tutar aralığı kademelerinden oluşan bir tarife tablosu olarak tanımlanmalıdır; her kademe (alt sınır, oran, sabit ücret) taşır, üst sınırı bir sonraki kademenin alt sınırıdır, son kademe açık uçludur.
- **FR-002**: Sistem tarife tablosunu bütün olarak doğrulamalıdır: en az bir kademe; ilk kademe alt sınırı 0; alt sınırlar kesin artan (boşluk/bindirme yapısal olarak imkânsız); her kademede oran 0–0,20 ve sabit ücret 0–100 TL aralığında; kademe sayısı üst sınırı aşılmaz. İhlalde kayıt oluşmaz, sorunlu kademe/alan bildirilir.
- **FR-003**: Efektif komisyon hesabında işlem tutarının düştüğü TEK kademe seçilmeli ve o kademenin oranı+sabiti tüm tutara uygulanmalıdır (dilimli birikimli hesap YAPILMAZ). Tam sınır değeri üst kademeye düşer.
- **FR-004**: Tarife güncelleme, tablonun bütününü yeni tabloyla değiştirir ve FR-002 doğrulamasından geçer; hatalı güncelleme mevcut tarifeyi değiştirmez.
- **FR-005**: Merchant-başına tekil-aktif politika kuralı ve durum makinesi (Active/Passive) aynen korunur; pasif kayıtlar tarifeleriyle listede görünür kalır (tarihçe).
- **FR-006**: Yönetim ekranı kademeli tarifeyi destekler: oluşturma/güncelleme formunda kademe satırı ekleme-çıkarma; listede politikanın kademeleri okunur biçimde gösterilir.
- **FR-007**: Efektif komisyon > işlem tutarı koruması kademeli hesapta da aynen çalışır.
- **FR-008**: Merchant kendi politikasını görüntülediğinde (mevcut self-servis okuma ucu) kademeli tarifeyi görür.

### Key Entities

- **CommissionPolicy (MEVCUT — değişir)**: Marjı tek (oran, sabit) çifti yerine kademe tablosu taşır. Kimlik, statü, tekil-aktif kuralı değişmez.
- **Kademe (YENİ değer kavramı)**: (alt sınır TL, oran, sabit ücret TL) üçlüsü; yalnız tarife tablosunun içinde yaşar, kendi başına kimliği yoktur.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Yönetici üç kademeli bir tarifeyi ekrandan tek oturuşta girebilir ve kayıt sonrası listede üç kademeyi de görür.
- **SC-002**: Aynı politika altında farklı kademelere düşen tutarlar için hesap sonuçları, elle hesapla birebir doğrulanabilir (örnek: 500 → 13,50; 1.000 → 21,00; 20.000 → 360,00).
- **SC-003**: Bozuk tarife girişlerinin %100'ü kayıt üretmeden, sorunlu kademeyi işaret eden hatayla döner.
- **SC-004**: Tek kademeli tarife girilen bir merchant'ın hesap sonuçları, önceki düz modelle girilecek aynı değerlerin sonuçlarından hiç sapmaz.

## Assumptions

- **Kademe seçimi "bracket" modelidir**: işlem tutarı hangi kademeye düşüyorsa o kademenin oranı+sabiti TÜM tutara uygulanır; vergi benzeri dilimli/birikimli hesap bilinçli olarak kapsam dışı (ödeme sektörü tarife pratiği; kullanıcı örneği de bu modeldeydi).
- Kademe sayısı üst sınırı 10 (makul tarife büyüklüğü; sabit doğrulama değeri).
- Tavanlar mevcut değerlerdir (oran 0,20; sabit 100 TL) ve kademe başına uygulanır.
- Tarihli tarife versiyonlama (eski işlemin eski tarifeyle mutabakatı) bu kapsamda YOK: henüz işlem akışı yaşamıyor, mutabakat yapacak veri yok; ihtiyaç ödeme akışı geldiğinde kendi spec'iyle ele alınır. Bugünkü tarihçe mekanizması (pasifleştir + yeni oluştur) yeterli kabul edilir.
- Mevcut dev verisindeki düz (tek oran) politika kayıtları dönüştürülmez; geliştirme aşaması kuralı gereği veri sıfırlanıp yeniden girilir (defansif migration yazılmaz).
- Para yuvarlama kuralı mevcut davranıştır (2 ondalık, AwayFromZero); kademe seçimi yuvarlamadan önce ham tutarla yapılır.