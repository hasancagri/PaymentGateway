# Feature Specification: Bank Referansı + Komisyon Grid

**Feature Branch**: `feat/bank-reference-commission-grid`

**Created**: 2026-07-31

**Status**: Draft

**Input**: User description: "Bank referans aggregate + tam CRUD (Commission.Api) ve BankCommission toplu-giriş grid'i. Banka = lookup verisi (Code 4 hane, Name, IsActive, SupportedInstallments listesi); dış-sistem entegrasyonu değil. Admin'de banka CRUD sayfası + komisyon grid: banka seçilince o bankanın desteklediği taksitlere göre CardBrand×CardType×TransactionRegion×taksit tüm kombinasyonları göster, dolu/eksik işaretle, toplu kaydet. Bankalar operatör tarafından elle girilir (seed yok)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Banka referansını yönet (Priority: P1)

Komisyon operatörü, sistemdeki bankaları merkezi bir listeden yönetir: mevcut bankaları
görür, kanonik bir banka **katalogundan** seçerek yeni banka ekler, bankanın desteklenen
taksitlerini/aktifliğini günceller ve artık kullanılmayan bankayı devre dışı bırakır veya siler.
Banka listesi seed edilmez (DB boş başlar, operatör ekler); ancak banka **adı ve kodu elle
yazılmaz** — operatör sabit bir katalogdan banka seçer, ad ve kod bu katalogdan gelir (immutable).

**Why this priority**: Banka listesi diğer her şeyin temelidir — komisyon girişi, filtreleme
ve eksik-combo hesabı bu kümeye dayanır. Onsuz komisyon grid'i anlamlı çalışamaz.

**Independent Test**: Operatör bankalar sayfasını açar (başta boş), katalogdan bir banka
seçip ekler/düzenler/siler; liste doğru güncellenir. Komisyon girişi olmadan tek başına test edilebilir.

**Acceptance Scenarios**:

1. **Given** hiç banka eklenmemiş, **When** operatör bankalar listesini açar, **Then** liste boştur
   ve "yeni banka ekle" seçeneği sunulur.
2. **Given** katalogda olup henüz eklenmemiş bir banka, **When** operatör onu katalog seçim
   kutusundan seçer ve taksit seti girer, **Then** banka listeye eklenir; adı ve kodu katalogdan gelir.
3. **Given** zaten eklenmiş bir banka, **When** operatör onu tekrar eklemeye çalışır, **Then**
   sistem "zaten var" hatası verir, kopya oluşmaz (katalog seçim kutusu eklenmişleri gizleyebilir).
4. **Given** bir banka, **When** operatör taksitlerini/aktifliğini değiştirir, **Then**
   değişiklik kaydedilir; banka adı ve kodu değiştirilemez (salt-görünüm).
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
6. **Given** kalabalık bir grid, **When** operatör marka/tip/bölge/taksit eksenlerinden birini/birkaçını
   filtreler, **Then** yalnız eşleşen satırlar görünür (filtreler birlikte daraltır), "hepsi" sıfırlar.
7. **Given** operatör bir oran girip "boşları doldur" der, **When** buton tıklanır, **Then** o an
   açık sayfadaki görünen (filtreyle daralmış ve geçerli sayfada) boş hücrelere o oran yazılır;
   dolu ve görünmeyen hücreler değişmez. Sonra normal kaydetme akışı çalışır.
8. **Given** çok satırlı bir grid, **When** grid yüklenir, **Then** satırlar sayfa başına 20 kayıt
   halinde sayfalanır; operatör önceki/sonraki ile gezinir. Kaydetme tüm doldurulan hücreleri kapsar
   (yalnız açık sayfayı değil).

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
2. **Given** komisyon kayıtları listesi, **When** operatör listeye bakar, **Then** her satırda banka
   ham kod yerine adıyla görünür.
3. **Given** çok kayıtlı komisyon listesi, **When** operatör marka/tip/bölge/taksit eksenlerinden
   filtreler ve sayfalar arası gezinir, **Then** yalnız eşleşen kayıtlar 20'li sayfalar halinde görünür.

---

### Edge Cases

- Banka desteklenen taksit listesi boş verilirse: reddedilir (en az bir taksit gerekir).
- Taksit değeri geçerli aralık dışında (1'den küçük veya üst sınırdan büyük): reddedilir.
- Pasif (IsActive=false) banka: komisyon grid'i banka seçiminde varsayılan olarak listelenmez.
- Grid'de bankanın desteklemediği bir taksit için oran gelirse: reddedilir.
- Aynı banka+kombinasyon iki kez gönderilirse (bulk içinde): tek kayıt, son değer geçerli.
- Debit/prepaid kartların taksitsiz doğası: v1'de grid tam gösterilir, kısıt uygulanmaz (operatör kararı).
- Katalogda olmayan bir banka kodu ile ekleme gelirse: reddedilir (yalnız katalogdaki bankalar eklenebilir).
- Katalogdaki tüm bankalar zaten eklenmişse: katalog seçim kutusu boş kalır (eklenecek banka yok).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem bankaları seed etmemeli; banka listesi boş başlar, operatör ekler. Ancak eklenecek
  bankalar sabit bir **kanonik katalogdan** seçilir — banka adı ve kodu serbest metin olarak girilmez.
- **FR-001a**: Sistem, seçilebilir bankaların kanonik katalogunu (banka kodu + adı) sunmalı. Operatör
  yalnızca bu isteğe göre eklenmemiş bankaları seçebilmeli (zaten eklenenler seçimden elenebilir).
- **FR-002**: Operatör yeni bankayı katalogdan seçerek ekleyebilmeli; her banka benzersiz 4 haneli koda
  sahip olmalı. Ad ve kod katalogdan gelir; operatör yalnızca desteklenen taksit setini belirler.
- **FR-003**: Sistem aynı kodla ikinci bir banka oluşturulmasını engellemeli. Katalogda bulunmayan bir
  kodla ekleme reddedilmeli.
- **FR-004**: Her banka bir ad (katalogdan), aktiflik durumu ve desteklenen taksit sayıları listesi taşımalı.
- **FR-005**: Desteklenen taksit listesi boş olamaz; her değer geçerli aralıkta (1..üst sınır) olmalı;
  tekrarlar tekilleştirilmeli. Operatör taksitleri 1..üst sınır arası seçim kutucuklarıyla belirler.
- **FR-006**: Operatör bankanın aktifliğini ve taksit listesini güncelleyebilmeli; banka adı ve kodu
  değiştirilememeli (ikisi de katalogdan gelir, salt-görünüm).
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
- **FR-015**: Grid ekranında operatör satırları kart markası / kart tipi / işlem bölgesi / taksit
  eksenlerine göre filtreleyebilmeli; filtreler birlikte daraltır ("hepsi" sıfırlar). Filtre yalnız
  görünümü etkiler, girilmiş/kaydedilmemiş oranları kaybettirmez.
- **FR-016**: Grid ekranında operatör bir oran girip tek işlemle o an açık sayfadaki görünen
  (filtreyle daralmış **ve** geçerli sayfada olan) **boş** hücrelerin tümünü o oranla doldurabilmeli;
  dolu ve o an görünmeyen (filtreli/başka sayfa) hücreler değişmemeli. Bu yalnız giriş kolaylığıdır;
  kalıcılık normal kaydetme akışıyla olur.
- **FR-017**: Komisyon kayıtları listesinde banka, ham kod yerine banka **adıyla** gösterilmeli
  (ad çözülemezse koda düşülür).
- **FR-019**: Komisyon kayıtları listesinde operatör satırları kart markası / kart tipi / işlem bölgesi
  / taksit eksenlerine göre filtreleyebilmeli (filtreler birlikte daraltır) ve liste sayfa başına 20
  kayıt olacak şekilde sayfalanmalı. Banka seçimi mevcut şekliyle (liste düzeyinde) korunur.
- **FR-018**: Grid, satırları sayfa başına 20 kayıt olacak şekilde sayfalamalı; operatör sayfalar
  arasında gezinebilmeli. Sayfalama yalnız görünümü etkiler; girilmiş/kaydedilmemiş oranları ve
  kaydetme kapsamını (tüm doldurulan hücreler) değiştirmez.
- **FR-014**: Banka referansı yalnızca lookup/katalog verisidir; banka POS kimlik bilgileri, limitler
  veya komisyon oranları burada tutulmaz (ilgili başka kayıtların sorumluluğu).

### Key Entities *(include if feature involves data)*

- **Bank**: Sistemin tanıdığı bir bankanın referans/katalog kaydı. Nitelikler: benzersiz banka kodu
  (4 hane, değiştirilemez), ad (katalogdan, değiştirilemez), aktiflik durumu, desteklenen taksit
  sayıları listesi. Komisyon kombinasyonlarının filtre kümesi ve "eksik" hesabının referansıdır.
- **Bank Catalog** (yeni, kanonik referans): Sistemde seçilebilir bankaların sabit listesi — her giriş
  banka kodu + adı. Değiştirilemez/kalıcı veri değil, uygulamaya gömülü sabit katalog. Operatörün banka
  eklerken seçtiği kaynak; Bank kaydının ad ve kodu buradan türer. Seçilebilir bankaların otoritesidir.
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
- **SC-007**: Operatör banka eklerken banka adı/kodu yazmak zorunda kalmaz; katalogdan seçer ve ad/kod
  otomatik gelir (yanlış/tutarsız kod-ad girişi imkânsız).
- **SC-008**: Operatör grid'i bir eksene göre filtreleyip görünen boş hücrelerin tümünü tek işlemle
  doldurabilir (hücre başına ayrı giriş gerekmeden).

## Assumptions

- Tek bir operatör rolü vardır; yetkilendirme bu dilimde uygulanmaz (proje geneli ertelenmiş).
- Banka listesi seed edilmez (DB boş başlar); operatör bankaları katalogdan seçerek ekler. Kanonik
  katalog (banka kodu + adı) mevcut sanal POS kütüphanesindeki listeden türetilmiş, uygulamaya gömülü
  sabit veridir; sanal POS kütüphanesine çalışma-zamanı bağımlılığı yoktur.
- Desteklenen taksit üst sınırı, mevcut ödeme kısıtıyla tutarlıdır (1..15).
- Yeni bankalar için varsayılan taksit seti yaygın kullanılan değerlerdir (ör. 1,2,3,6,9,12);
  operatör düzenleyebilir.
- Banka bazlı taksit modeli v1 için yeterlidir; kart programı/BIN bazlı taksit uygunluğu ayrı bir
  dilimdir (kapsam dışı).
- Debit/prepaid taksit kısıtı bu dilimde uygulanmaz; grid tam gösterilir.