# Feature Specification: Merchant SubMerchant Model

**Feature Branch**: `023-merchant-submerchant-model`

**Created**: 2026-08-13

**Status**: Draft

**Input**: User description: "Merchant BC'yi iyzico SubMerchant (pazaryeri) modeliyle sıfırdan kur. Merchant aggregate'i iyzico SubMerchant sözleşmesiyle hizalı alan setiyle (YAGNI); zengin aggregate + statü makinesi + MerchantKey üretimi. Vertical slice CRUD + statü ucu (scope policy'li). 012 Identity zinciri yeniden bağlanır (merchant.lifecycle → OpenIddict senkron). İyzico'ya gerçek SubMerchant çağrısı YOK (ayrı iş). Admin UI ayrı iş. Saf domain birim testleri yazılır. CLAUDE.md kurallarına tam uyum."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant kaydı oluşturulur ve yönetilir (Priority: P1)

Operatör (admin düzlemi), pazaryerine katılacak bir satıcıyı iyzico alt-üye-işyeri
sözleşmesiyle hizalı bilgilerle kaydeder: ad/iletişim (ad, e-posta, GSM), yasal kimlik
(işyeri tipi — şahıs/şahıs şirketi/sermaye şirketi — ve tipe uygun kimlik-no/vergi
bilgileri/unvan), ödeme bilgisi (IBAN, adres). Kayıt oluşturulduğunda sistem merchant'a
benzersiz bir kimlik ve MerchantKey (makine erişim sırrı) üretir. Operatör kaydı
güncelleyebilir, listeleyebilir ve tekil görüntüleyebilir.

**Why this priority**: Merchant kaydı BC'nin varlık sebebi; diğer her şey (statü, kimlik
zinciri, ileride iyzico kaydı ve komisyon) bu kaydın üstüne kurulur.

**Independent Test**: API üzerinden merchant oluştur → tekil getir → güncelle → listede
gör; alan doğrulamaları (tip-uyumlu zorunlu alanlar, IBAN biçimi, e-posta biçimi)
ihlallerde anlamlı hata döner.

**Acceptance Scenarios**:

1. **Given** geçerli merchant bilgileri, **When** operatör kayıt oluşturur, **Then**
   merchant benzersiz kimlik + MerchantKey ile kaydolur ve tekil sorguda görünür
   (MerchantKey yalnız oluşturma yanıtında bir kez döner).
2. **Given** işyeri tipi "şahıs", **When** vergi-şirket alanları (unvan/vergi no)
   doldurulmadan kayıt yapılır, **Then** kayıt başarılıdır; **Given** tip "sermaye
   şirketi", **When** unvan/vergi bilgileri eksikse, **Then** kayıt tip-uyum hatasıyla
   reddedilir.
3. **Given** bozuk IBAN veya e-posta, **When** kayıt/güncelleme denenir, **Then** alan
   bazlı doğrulama hatası döner, kayıt değişmez.
4. **Given** mevcut merchant, **When** bilgileri güncellenir, **Then** değişiklik tekil
   sorguda görünür; kimlik ve MerchantKey değişmez.

---

### User Story 2 - Statü yönetimi ve kimlik zinciri (Priority: P2)

Operatör merchant'ı Active/Passive/Suspended statüleri arasında değiştirir. Merchant
oluşturulduğunda ve statüsü değiştiğinde sistem bunu kimlik servisine duyurur; kimlik
servisi merchant'ın makine istemcisini (MerchantKey sırrıyla) senkronlar. Token verme
statü-kapılıdır: yalnız Active merchant token alabilir (012 davranışı aynen).

**Why this priority**: Merchant'ın gateway'e makine erişimi (kendi kaydını okuma vb.)
bu zincire bağlı; 012'de KARARLI olan düzlem yeniden yaşamalı.

**Independent Test**: Merchant oluştur → kimlik servisinde istemcinin doğduğunu doğrula →
Active iken token alınır → Passive yap → token reddedilir.

**Acceptance Scenarios**:

1. **Given** yeni merchant (varsayılan Active), **When** oluşturma tamamlanır, **Then**
   kimlik servisi istemci kaydını oluşturur ve merchant kendi kimliği + MerchantKey ile
   token alabilir.
2. **Given** Active merchant, **When** operatör statüyü Passive/Suspended yapar, **Then**
   duyuru kimlik servisine ulaşır ve sonraki token istekleri reddedilir.
3. **Given** statü değişikliği, **When** kayıt işlemi başarısız olursa, **Then** duyuru
   yayınlanmaz (kayıt ve duyuru atomiktir — yarım durum oluşmaz).
4. **Given** statü ucu, **When** merchant'ın kendi token'ı ile çağrılır, **Then**
   reddedilir (statü yönetimi yalnız admin düzlemi).

---

### User Story 3 - Domain kuralları test güvencesinde (Priority: P3)

Geliştirici merchant domain kurallarını (tip-uyum doğrulaması, IBAN/e-posta biçimi,
statü geçişleri, MerchantKey üretimi/değişmezliği) saf birim testleriyle doğrular;
test projesi çözümde yeniden doğar ve `dotnet test` yeşildir.

**Why this priority**: 022'de test kalmamıştı; domain kurallarının kanıtı olmadan
sonraki spec'ler (iyzico kaydı, komisyon) güvenle inşa edilemez.

**Independent Test**: `dotnet test` — merchant domain testleri koşar ve geçer; dış
bağımlılık (DB/HTTP) gerektirmez.

**Acceptance Scenarios**:

1. **Given** test projesi, **When** `dotnet test` koşulur, **Then** aggregate davranış
   testleri (oluşturma, tip-uyum, doğrulamalar, statü geçişleri) yeşildir.
2. **Given** testler, **When** incelenir, **Then** hiçbir test veritabanı/ağ bağımlılığı
   içermez (saf domain — proje konvansiyonu).

---

### Edge Cases

- Aynı e-posta ile ikinci merchant: benzersizlik aranmaz (iyzico tarafında
  SubMerchantExternalId eşlemesi ileride; bu fazda e-posta benzersizliği zorunlu değil —
  varsayım bölümünde).
- Statü ucunda aynı statüye tekrar geçiş: işlem idempotent kabul edilir, hata değil
  (duyuru tekrarı zararsız — kimlik senkronu idempotent).
- MerchantKey: oluşturmadan sonra hiçbir sorgu yanıtında görünmez (sır sızdırma yasağı,
  012/018 kararlarıyla uyumlu); kayıp anahtar/rotasyon bu kapsamda değil.
- İyzico SubMerchantKey alanı: bu fazda hep boş — dolumu iyzico kayıt entegrasyonunun
  (ayrı iş) çıktısı; boşluğu hiçbir akışı engellemez.
- Silme: fiziksel silme yok; bu fazda silme ucu da yok (statü ile pasifleştirme yeter —
  YAGNI).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Merchant kaydı iyzico alt-üye-işyeri sözleşmesiyle hizalı alan setini
  taşımalıdır: ad, e-posta, GSM, adres, IBAN, işyeri tipi (şahıs / şahıs şirketi /
  sermaye şirketi), tipe bağlı yasal alanlar (kimlik no; vergi dairesi + vergi no +
  unvan), iletişim ad/soyad ve sağlayıcı alt-üye anahtarı (bu fazda boş). Bu setin
  dışında alan eklenmez (YAGNI); para birimi alanı sabittir (yalnız TL — anayasa).
- **FR-002**: İş kuralları kayıt varlığının içinde yaşamalıdır: tip-uyum doğrulaması
  (şahıs → kimlik no zorunlu; şirket tipleri → vergi bilgileri/unvan zorunlu), IBAN
  biçim doğrulaması, e-posta biçim doğrulaması, statü geçiş kuralları. İhlaller beklenen
  hata olarak (istisnasız sonuç sözleşmesi) döner.
- **FR-003**: Oluşturmada sistem benzersiz kimlik ve MerchantKey (makine erişim sırrı)
  üretmelidir; MerchantKey yalnız oluşturma yanıtında bir kez döner, sonraki hiçbir
  sorguda görünmez ve değiştirilemez (rotasyon ayrı iş).
- **FR-004**: CRUD yüzeyi sunulmalıdır: oluştur, güncelle, tekil getir, listele + ayrı
  statü değiştirme ucu. Uçlar yetki beyanlıdır: okuma uçları okuma yetkisi, yazma uçları
  yazma yetkisi ister; statü ucu yalnız admin düzlemine açıktır (merchant'ın kendi
  token'ı giremez). Merchant kendi kaydını kendi token'ıyla okuyabilir (tenant sınırı:
  başkasınınkini okuyamaz).
- **FR-005**: Merchant oluşturma ve statü değişikliği, kimlik servisine duyurulmalıdır
  (mevcut yaşam-döngüsü sözleşmesi ve tüketicisi aynen kullanılır); duyuru kayıt
  işlemiyle atomiktir (başarısız işlemde duyuru çıkmaz). Kimlik servisi istemciyi
  senkronlar; token verme statü-kapılıdır (yalnız Active).
- **FR-006**: İyzico'ya gerçek alt-üye-işyeri kaydı bu kapsamda YAPILMAZ; sağlayıcı
  istek/model tipleri (022 malzemesi) hammadde olarak kalır, entegrasyon ayrı iştir.
- **FR-007**: Merchant domain kuralları saf birim testleriyle (DB/ağ bağımlılıksız)
  doğrulanmalıdır; test projesi çözüme eklenir ve derleme/test koşusu yeşildir.
- **FR-008**: Yapı proje kurallarına tam uyar: aggregate-klasör düzeni, vertical slice
  (bir feature = bir dosya), istisnasız sonuç sözleşmesi, metot-üstü handler notu,
  private-helper yasağı, strongly-typed config (gerekirse).

### Key Entities

- **Merchant (yeni aggregate)**: pazaryeri satıcısı — iyzico alt-üye sözleşmesi hizalı
  kimlik/iletişim/yasal/ödeme alanları + statü (Active/Passive/Suspended) + MerchantKey
  + boş sağlayıcı anahtarı. Zengin davranış: oluşturma fabrikası, güncelleme, statü
  geçişleri, tip-uyum ve biçim doğrulamaları.
- **İşyeri tipi**: şahıs / şahıs şirketi / sermaye şirketi — hangi yasal alanların
  zorunlu olduğunu belirler.
- **Yaşam-döngüsü duyuruları (mevcut sözleşme)**: oluşturma ve statü değişikliği;
  kimlik servisi istemci senkronunun girdisi.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operatör tek istekle merchant oluşturabilir; oluşturma-okuma-güncelleme-
  listeleme döngüsü uçtan uca %100 tamamlanır.
- **SC-002**: Tip-uyum ve biçim ihlallerinin %100'ü alan-bazlı anlamlı hatayla reddedilir;
  hiçbir ihlal kayıt üretmez.
- **SC-003**: Merchant oluşturma/statü değişikliği sonrası kimlik servisi istemci durumu
  senkrondur: Active merchant token alır, Passive/Suspended merchant'ın token isteği
  reddedilir (canlı doğrulama senaryosu).
- **SC-004**: MerchantKey oluşturma yanıtı dışında hiçbir çıktıda görünmez.
- **SC-005**: Çözüm sıfır hatayla derlenir; yeni domain test projesi `dotnet test` ile
  yeşildir.

## Assumptions

- Yeni merchant varsayılan statüsü **Active** (012'nin Provisioning/aktivasyon zinciri
  013 onboarding'e aitti ve söküldü; onboarding/aktivasyon akışı ileride ayrı spec olarak
  dönebilir — bu faz basit admin-oluşturmalı model).
- E-posta/IBAN benzersizliği zorunlu değil (dev fazı; iyzico entegrasyonunda
  SubMerchantExternalId = merchant kimliği eşlemesi kurulunca yeniden değerlendirilir).
- Listeleme basit tam-liste (sayfalama yok — mevcut ölçek; gerekirse sonra).
- Admin UI bağlanmaz; tüketici şimdilik doğrudan API (canlı doğrulama token'lı istekle).
- Kimlik servisindeki mevcut tüketici (istemci senkron handler'ı) ve yaşam-döngüsü
  sözleşmesi değişmeden kullanılır; sözleşmede alan eklemesi gerekmez.
- Merchant'ın kendi kaydını okuma ucu merchant token'ına açık (012 tenant kuralı);
  bunun dışındaki merchant-self yüzeyi (güncelleme vb.) admin düzleminde kalır.
- Canlı doğrulama kapsamı: Aspire ile kimlik zinciri senaryosu (SC-003) — iyzico'ya ağ
  çağrısı yok.
