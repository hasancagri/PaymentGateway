# Feature Specification: Bank Referansı + Komisyon Grid

**Feature Branch**: `feat/bank-reference-commission-grid`

**Created**: 2026-07-31

**Status**: Draft

**Input**: User description: "Bank referans aggregate + tam CRUD (Commission.Api) ve BankCommission toplu-giriş grid'i. Banka = lookup verisi (Code 4 hane, Name, IsActive, SupportedInstallments listesi); dış-sistem entegrasyonu değil. Admin'de banka CRUD sayfası + komisyon grid: banka seçilince o bankanın desteklediği taksitlere göre CardBrand×CardType×TransactionRegion×taksit tüm kombinasyonları göster, dolu/eksik işaretle, toplu kaydet. Bankalar operatör tarafından elle girilir (seed yok)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Banka referansını yönet (Priority: P1)

Komisyon operatörü, sistemdeki bankaları merkezi bir listeden yönetir: mevcut bankaları
görür, yeni banka ekler, banka adını/desteklenen taksitlerini/aktifliğini günceller ve
artık kullanılmayan bankayı devre dışı bırakır veya siler. Banka listesi operatör tarafından
elle oluşturulur (önceden dolu gelmez).

**Why this priority**: Banka listesi diğer her şeyin temelidir — komisyon girişi, filtreleme
ve eksik-combo hesabı bu kümeye dayanır. Onsuz komisyon grid'i anlamlı çalışamaz.

**Independent Test**: Operatör bankalar sayfasını açar (başta boş), bir banka
ekler/düzenler/siler; liste doğru güncellenir. Komisyon girişi olmadan tek başına test edilebilir.

**Acceptance Scenarios**:

1. **Given** hiç banka eklenmemiş, **When** operatör bankalar listesini açar, **Then** liste boştur
   ve "yeni banka ekle" seçeneği sunulur.
2. **Given** listede olmayan bir banka, **When** operatör kod (4 hane) + ad + taksit seti girer,
   **Then** banka listeye eklenir.
3. **Given** var olan bir bankayla aynı kod, **When** operatör tekrar eklemeye çalışır, **Then**
   sistem "zaten var" hatası verir, kopya oluşmaz.
4. **Given** bir banka, **When** operatör adını/taksitlerini/aktifliğini değiştirir, **Then**
   değişiklik kaydedilir; banka kodu değiştirilemez.
5. **Given** hiçbir komisyonu olmayan banka, **When** operatör siler, **Then** banka listeden
   kalkar (yumuşak silme).
6. **Given** bağlı komisyon kayıtları olan banka, **When** operatör silmeye çalışır, **Then**
   sistem engeller ve "önce komisyonları sil" der.

---

### User Story 2 - Bir banka için tüm komisyon kombinasyonlarını doldur (Priority: P1)

Komisyon operatörü, seçtiği bir banka için olası tüm komisyon kombinasyonlarını (kart markası ×
kart tipi × işlem bölgesi × bankanın desteklediği taksit) tek ekranda görür. Hangi hücrelerin
dolu, hangilerinin eksik olduğunu anında ayırt eder, eksikleri doldurur ve hepsini tek işlemde
kaydeder. Amaç: bankaya ait hiçbir kombinasyonun komisyonsuz (eksik) kalmaması.

**Why this priority**: Kullanıcının asıl acısı bu — dağınık tek-tek giriş ve görünmeyen boşluklar.
Boşluk = ödeme akışında o kombinasyon için fiyat üretilememesi.

**Independent Test**: Operatör banka seçer, tam kombinasyon grid'ini görür (dolu değerler önceden
gelir, boşlar işaretli), birkaç boş hücreyi doldurup kaydeder; kayıt sonrası o hücreler dolu görünür.

**Acceptance Scenarios**:

1. **Given** desteklenen taksitleri olan bir banka, **When** operatör onu seçer, **Then** grid
   marka × tip × bölge × taksit'in tüm kombinasyonlarını satır olarak gösterir.
2. **Given** bazı kombinasyonların önceden oranı var, **When** grid yüklenir, **Then** dolu hücreler
   oranıyla gelir, boş hücreler görsel olarak "eksik" işaretlenir.
3. **Given** operatör bazı boş hücrelere oran girdi, **When** kaydeder, **Then** yalnız girilen
   hücreler eklenir/güncellenir; dokunulmayanlar değişmez.
4. **Given** var olan bir kombinasyona yeni oran girildi, **When** kaydeder, **Then** o kombinasyonun
   oranı güncellenir (kopya oluşmaz).
5. **Given** grid tam dolu, **When** operatör tekrar bakar, **Then** hiçbir hücre "eksik" değildir.

---

### User Story 3 - Eksik kapsamı gör ve filtrele (Priority: P3)

Operatör, hangi bankaların/kombinasyonların hâlâ eksik komisyona sahip olduğunu görebilir; bir
bankaya göre filtreleyerek yalnız ilgili kayıtlara odaklanır.

**Why this priority**: Kapsam bütünlüğünü izlemek için faydalı ama grid'in kendisi eksikleri zaten
gösterdiğinden ikincil.

**Independent Test**: Operatör banka koduna göre filtreler, yalnız o bankanın komisyonları listelenir.

**Acceptance Scenarios**:

1. **Given** birden çok bankanın komisyonları, **When** operatör bir bankaya göre filtreler, **Then**
   yalnız o bankanın kayıtları görünür.

---

### Edge Cases

- Banka desteklenen taksit listesi boş verilirse: reddedilir (en az bir taksit gerekir).
- Taksit değeri geçerli aralık dışında (1'den küçük veya üst sınırdan büyük): reddedilir.
- Pasif (IsActive=false) banka: komisyon grid'i banka seçiminde varsayılan olarak listelenmez.
- Grid'de bankanın desteklemediği bir taksit için oran gelirse: reddedilir.
- Aynı banka+kombinasyon iki kez gönderilirse (bulk içinde): tek kayıt, son değer geçerli.
- Debit/prepaid kartların taksitsiz doğası: v1'de grid tam gösterilir, kısıt uygulanmaz (operatör kararı).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem bankaları önceden yüklememeli; banka listesi operatör tarafından elle oluşturulur
  (boş başlar).
- **FR-002**: Operatör yeni banka ekleyebilmeli; her banka benzersiz 4 haneli koda sahip olmalı.
- **FR-003**: Sistem aynı kodla ikinci bir banka oluşturulmasını engellemeli.
- **FR-004**: Her banka bir ad, aktiflik durumu ve desteklenen taksit sayıları listesi taşımalı.
- **FR-005**: Desteklenen taksit listesi boş olamaz; her değer geçerli aralıkta (1..üst sınır) olmalı;
  tekrarlar tekilleştirilmeli.
- **FR-006**: Operatör bankanın adını, aktifliğini ve taksit listesini güncelleyebilmeli; banka kodu
  değiştirilememeli.
- **FR-007**: Operatör bankayı silebilmeli (yumuşak silme); bankaya bağlı komisyon kaydı varsa silme
  engellenmeli ve açıklayıcı hata dönmeli.
- **FR-008**: Operatör bankaları listeleyebilmeli ve tek bir bankayı kodla görüntüleyebilmeli.
- **FR-009**: Operatör bir banka seçtiğinde sistem, o bankanın desteklediği taksitlere göre kart
  markası × kart tipi × işlem bölgesi × taksit'in tüm kombinasyonlarını grid olarak sunmalı.
- **FR-010**: Grid, her kombinasyon için mevcut komisyon oranını göstermeli; oranı olmayan (eksik)
  kombinasyonları görsel olarak ayırt etmeli.
- **FR-011**: Operatör grid'de birden çok kombinasyon için oran girip hepsini tek işlemde
  kaydedebilmeli (toplu ekle/güncelle); var olan kombinasyon güncellenmeli, olmayan eklenmelidir.
- **FR-012**: Toplu kayıt, seçilen bankaya ve onun desteklediği taksitlere ait olmayan girdileri
  reddetmeli.
- **FR-013**: Operatör komisyon kayıtlarını banka koduna göre filtreleyebilmeli.
- **FR-014**: Banka referansı yalnızca lookup/katalog verisidir; banka POS kimlik bilgileri, limitler
  veya komisyon oranları burada tutulmaz (ilgili başka kayıtların sorumluluğu).

### Key Entities *(include if feature involves data)*

- **Bank**: Sistemin tanıdığı bir bankanın referans/katalog kaydı. Nitelikler: benzersiz banka kodu
  (4 hane, değiştirilemez), ad, aktiflik durumu, desteklenen taksit sayıları listesi. Komisyon
  kombinasyonlarının filtre kümesi ve "eksik" hesabının referansıdır.
- **Bank Commission** (mevcut): Bir bankanın belirli bir kombinasyon (marka × tip × bölge × taksit)
  için oranı. Bu özellik onu banka bazında toplu doldurmayı ve eksikleri görünür kılmayı ekler.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operatör bir bankayı ekledikten sonra onu listede görür ve komisyon grid'inde banka
  olarak seçebilir.
- **SC-002**: Operatör bir banka için tüm komisyon kombinasyonlarını tek ekranda görür ve eksik
  olanları bakışta ayırt eder.
- **SC-003**: Operatör bir bankanın tüm eksik kombinasyonlarını tek kaydetme işlemiyle
  tamamlayabilir (kombinasyon başına ayrı işlem gerektirmeden).
- **SC-004**: Bir bankanın komisyon kapsamı %100 dolduğunda grid hiçbir "eksik" hücre göstermez.
- **SC-005**: Bağlı komisyonu olan bir banka yanlışlıkla silinemez (veri bütünlüğü korunur).
- **SC-006**: Aynı banka kodu veya aynı banka+kombinasyon için kopya kayıt oluşmaz.

## Assumptions

- Tek bir operatör rolü vardır; yetkilendirme bu dilimde uygulanmaz (proje geneli ertelenmiş).
- Banka listesi seed edilmez; operatör bankaları elle girer. Banka kodları/adları için mevcut sanal
  POS kütüphanesindeki liste yalnızca operatöre referans olabilir (sistemde otomatik yükleme yok).
- Desteklenen taksit üst sınırı, mevcut ödeme kısıtıyla tutarlıdır (1..15).
- Yeni bankalar için varsayılan taksit seti yaygın kullanılan değerlerdir (ör. 1,2,3,6,9,12);
  operatör düzenleyebilir.
- Banka bazlı taksit modeli v1 için yeterlidir; kart programı/BIN bazlı taksit uygunluğu ayrı bir
  dilimdir (kapsam dışı).
- Debit/prepaid taksit kısıtı bu dilimde uygulanmaz; grid tam gösterilir.