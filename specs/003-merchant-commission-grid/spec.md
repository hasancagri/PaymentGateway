# Feature Specification: Merchant Komisyon Grid

**Feature Branch**: `feat/merchant-commission-grid`

**Created**: 2026-08-01

**Status**: Draft

**Input**: User description: "Merchant komisyon grid (003). 002'deki banka komisyon grid'inin merchant karşılığı. Operatör her merchant için CardBrand×CardType×TransactionRegion×taksit kombinasyonlarına komisyon oranı girer; oranı girerken aynı kombinasyondaki banka oranlarını da görür ve bilinçli değer belirler. Merchant komisyonu taksit-başına, tek bankaya değil kombinasyona bağlı; oran serbest ama banka tavanının altına düşerse işaretlenir (hard block yok)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant komisyonlarını grid ile yönet (Priority: P1)

Komisyon operatörü bir merchant seçer ve o merchant'ın komisyon oranlarını bir grid
üzerinden toplu olarak yönetir. Grid, komisyonun uygulandığı tüm kombinasyonları
(kart markası × kart tipi × işlem bölgesi × taksit sayısı) satır satır listeler; dolu
ve eksik hücreler görsel olarak ayrışır. Operatör istediği hücrelere oran girer ve
tek işlemde toplu kaydeder.

**Why this priority**: Merchant komisyonu gateway'in gelir tarafıdır; bu grid olmadan
merchant başına oran yönetilemez. Feature'ın çekirdek değeri budur.

**Independent Test**: Bir merchant seçilir, grid açılır (başta tüm hücreler eksik),
birkaç kombinasyona oran girilip toplu kaydedilir; tekrar açıldığında değerler dolu
ve eksikler işaretli gelir. Banka oranı görünümü olmadan tek başına test edilebilir.

**Acceptance Scenarios**:

1. **Given** seçili merchant'ın hiç komisyonu yok, **When** operatör grid'i açar,
   **Then** tüm kombinasyon satırları "eksik" işaretiyle listelenir.
2. **Given** açık grid, **When** operatör bazı hücrelere oran girer ve toplu kaydeder,
   **Then** girilen oranlar kaydedilir; boş bırakılanlar eksik kalır.
3. **Given** zaten oranı olan bir kombinasyon, **When** operatör değeri değiştirip
   kaydeder, **Then** yeni oran öncekinin yerine geçer (aynı merchant+kombinasyon tek satır).
4. **Given** operatör bir hücreye sıfır veya negatif oran girer, **When** kaydetmeye
   çalışır, **Then** sistem reddeder ("oran sıfırdan büyük olmalı").
5. **Given** grid'de çok sayıda satır, **When** operatör listeyi gezer, **Then** satırlar
   sayfalanmış gelir (sayfa başına 20).

---

### User Story 2 - Oran girerken banka tavanını gör (Priority: P1)

Operatör bir kombinasyona merchant oranı girerken, aynı kombinasyonu servisleyebilen
bankaların komisyon oranı aralığını (en düşük–en yüksek) satır içinde görür. Girdiği
merchant oranı o kombinasyondaki en yüksek banka oranının altına düşerse (veya eşitse),
satır görsel olarak işaretlenir (ör. kırmızı) — çünkü bu, pahalı rotada gateway'in
zarar edebileceği anlamına gelir. İşaret uyarıdır; kayıt engellenmez (gateway bilinçli
olarak düşük oran belirleyebilir).

**Why this priority**: Merchant bankayı seçmez; işlem, kombinasyonu servisleyen
bankalardan birine yönlenir. Operatörün margin riskini görmeden oran girmesi sessiz
zarara yol açar. Banka tavanının görünürlüğü kararın çekirdeğidir.

**Independent Test**: Belirli bir kombinasyon için bilinen banka oranları varken grid
açılır; satırda doğru en-düşük/en-yüksek aralık görünür. Merchant oranı tavanın altına
girilince satır işaretlenir, üstüne çıkınca işaret kalkar.

**Acceptance Scenarios**:

1. **Given** bir kombinasyonu iki banka farklı oranlarla servisliyor, **When** operatör
   o satırı görür, **Then** en-düşük ve en-yüksek banka oranı satır içinde gösterilir.
2. **Given** merchant oranı o kombinasyonun en yüksek banka oranından büyük, **When**
   satır görüntülenir, **Then** satır normal (işaretsiz) görünür.
3. **Given** merchant oranı en yüksek banka oranına eşit veya altında, **When** satır
   görüntülenir, **Then** satır "banka tavanının altında" işaretlenir; ama operatör yine
   de kaydedebilir.
4. **Given** bir kombinasyonu hiçbir banka servislemiyor, **When** satır görüntülenir,
   **Then** banka aralığı yerine "banka yok" gösterilir ve oran işaretlenmez (serbest).
5. **Given** operatör bir merchant oranını kaydettikten sonra o kombinasyondaki bir
   banka oranı yükseltilir, **When** operatör grid'i yeniden açar, **Then** işaret
   güncel banka oranlarına göre yeniden hesaplanır (bayat değil, hep taze).

---

### User Story 3 - Grid'i eksenlere göre filtrele ve boşları doldur (Priority: P2)

Operatör büyük grid'i daraltmak için eksenlere (kart markası, kart tipi, işlem bölgesi,
taksit) göre filtreler; yalnız eşleşen satırlar görünür. Ayrıca "boşları doldur"
yardımıyla görünen satırlardaki boş oran alanlarına tek değeri toplu yazar.

**Why this priority**: Kombinasyon sayısı bir merchant için yüzlerce satıra çıkar
(marka×tip×bölge×taksit). Filtre ve toplu doldur olmadan giriş zahmetlidir, ama çekirdek
değer P1'de teslim edildiği için bu bir üretkenlik katmanıdır.

**Independent Test**: Grid açılır, bir eksen filtresi uygulanır → yalnız eşleşen satırlar
kalır; "boşları doldur" bir değer alır → görünen boş alanlar o değerle dolar (dolu alanlar
korunur), ardından toplu kaydedilir.

**Acceptance Scenarios**:

1. **Given** açık grid, **When** operatör taksit=6 filtreler, **Then** yalnız 6 taksitli
   kombinasyon satırları görünür.
2. **Given** filtreli görünüm, **When** operatör "boşları doldur" ile bir oran uygular,
   **Then** yalnız görünen ve boş olan alanlar dolar; dolu alanlar değişmez.
3. **Given** filtreler uygulanmış, **When** operatör toplu kaydeder, **Then** yalnız
   girilen/değişen oranlar kaydedilir.

---

### Edge Cases

- **Aynı merchant + aynı kombinasyon iki kez**: Toplu kayıtta veya tek kayıtta çakışma
  olursa mevcut satır güncellenir (upsert), kopya oluşmaz.
- **Geçersiz oran**: Sıfır, negatif veya boş-olmayan-ama-geçersiz oran reddedilir.
- **Dormant kombinasyon**: Hiçbir bankanın servislemediği kombinasyona oran girilebilir;
  işaretlenmez. İleride o kombinasyona banka eklenirse işaret otomatik belirir.
- **Retroaktif banka değişimi**: Banka oranı sonradan yükselir/eklenir/silinir → merchant
  oranı değişmese de tavan-altı işareti yeniden hesaplanır.
- **Bilinmeyen merchant**: `merchantId` opak tanımlayıcıdır; sistem onu doğrulamak için
  merchant servisine senkron çağrı yapmaz. Var olmayan bir merchant'a oran teknik olarak
  girilebilir (bilinçli erteleme).
- **Boş grid gönderimi**: Hiç değer değişmeden yapılan toplu kayıt hatasız no-op'tur.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, bir merchant için komisyon oranını (kart markası × kart tipi ×
  işlem bölgesi × taksit sayısı) kombinasyonu bazında saklamalı; taksit sayısı zorunlu
  eksendir.
- **FR-002**: Bir merchant + kombinasyon çifti için en çok bir oran bulunmalı (benzersizlik);
  aynı çifte ikinci giriş mevcut oranı güncellemeli (upsert).
- **FR-003**: Sistem, oranın sıfırdan büyük olmasını zorunlu kılmalı; sıfır/negatif reddedilmeli.
- **FR-004**: Merchant komisyonu herhangi bir tek bankaya bağlı OLMAMALI; kombinasyona bağlıdır.
  Bir bankanın belirli bir taksiti desteklememesi merchant oranını bağlamaz.
- **FR-005**: Sistem, bir merchant'ın tüm komisyon satırlarını, her kombinasyon için o
  kombinasyonu servisleyen bankaların en-düşük ve en-yüksek oranıyla birlikte sunmalı.
- **FR-006**: Sistem, her satır için merchant oranının o kombinasyonun en yüksek banka
  oranına eşit veya altında olup olmadığını (tavan-altı işareti) belirtmeli. Bu işaret
  okuma anında güncel banka oranlarından hesaplanmalı, saklanmamalı.
- **FR-007**: Tavan-altı durumu kaydı ENGELLEMEMELİ; yalnız işaret/uyarı olmalı (gateway
  bilinçli olarak tavan-altı oran belirleyebilir).
- **FR-008**: Bir kombinasyonu hiçbir banka servislemiyorsa banka aralığı boş ("banka yok")
  gösterilmeli ve o satır tavan-altı işaretlenmemeli.
- **FR-009**: Sistem, bir merchant için toplu (çok kombinasyonlu) oran girişini/güncellemesini
  tek atomik işlemde desteklemeli.
- **FR-010**: Sistem, tek bir kombinasyon için oran oluşturma ve güncellemeyi de desteklemeli.
- **FR-011**: Operatör grid'i eksenlere göre (kart markası, kart tipi, işlem bölgesi, taksit)
  filtreleyebilmeli; yalnız eşleşen satırlar görünmeli.
- **FR-012**: Operatör görünen boş oran alanlarına tek değeri toplu yazabilmeli ("boşları
  doldur"); dolu alanlar korunmalı.
- **FR-013**: Grid, eksik (oranı girilmemiş) kombinasyonları görsel olarak işaretlemeli.
- **FR-014**: Grid satırları sayfalanmalı (sayfa başına 20).
- **FR-015**: Operatör bir merchant'ı listeden seçebilmeli; merchant listesi merchant
  kayıtlarının tutulduğu kaynaktan gelmeli.
- **FR-016**: Grid'in eksen seçenekleri (marka/tip/bölge/taksit) tek kaynaktan gelmeli;
  arayüz bu değerleri kendi içinde kopyalamamalı.
- **FR-017**: Sistem banka kodu bazlı bir filtre SUNMAMALI (bilinçli kapsam-dışı).
- **FR-018**: Taksit ekseni 1'den 15'e kadar değerleri kapsamalı.

### Key Entities *(include if feature involves data)*

- **MerchantCommission**: Bir merchant'ın belirli bir kombinasyon için gateway'e ödediği
  komisyon oranı. Nitelikler: merchant tanımlayıcısı (opak), kombinasyon (kart markası,
  kart tipi, işlem bölgesi, taksit sayısı), oran. Herhangi bir bankaya doğrudan bağlı değil.
- **Kombinasyon (Criteria)**: Komisyonun uygulandığı kart markası × kart tipi × işlem
  bölgesi × taksit sayısı dörtlüsü; değer bazlı benzersizlik taşır.
- **Banka oran aralığı (türetilmiş, saklanmaz)**: Bir kombinasyon için o kombinasyonu
  servisleyen banka komisyonlarının en-düşük ve en-yüksek oranı; okuma anında hesaplanır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operatör bir merchant için tüm komisyon oranlarını tek grid ekranında, tek
  toplu kayıt işlemiyle girebilir (kombinasyon başına ayrı ekran/işlem gerekmez).
- **SC-002**: Operatör, oran girerken her satırda ilgili banka oran aralığını görür;
  tavan-altı oranlar %100 doğrulukla işaretlenir (bilinen banka oranlarına karşı test edilir).
- **SC-003**: Banka oranı değiştikten sonra grid yeniden açıldığında tavan-altı işaretleri
  güncel banka oranlarını yansıtır (bayat işaret oranı %0).
- **SC-004**: Bir kombinasyonu hiçbir banka servislemediğinde operatör serbestçe oran girebilir
  ve bu satır hatalı olarak işaretlenmez.
- **SC-005**: Filtre + boşları-doldur ile operatör, yüzlerce kombinasyonlu bir merchant için
  oran girişini tek tek girmeye kıyasla belirgin biçimde daha az adımda tamamlar.

## Assumptions

- Merchant oranı taksit-başına belirlenir (peşin ve her taksit kademesi ayrı oran). Eski
  sistemdeki taksitsiz model bilinçli olarak terk edildi.
- Banka komisyonları (BankCommission) 002'de tanımlı; merchant grid'i onları salt-okunur
  referans olarak kullanır, değiştirmez.
- Merchant kayıtları ayrı bir kaynakta tutulur; bu feature merchant'ı yalnızca opak bir
  tanımlayıcıyla ilişkilendirir, doğrulama için senkron çağrı yapmaz (bilinçli erteleme).
- Yetkilendirme yoktur; ekranlar şimdilik korumasızdır (Identity BC ile gelecek).
- Başlangıç verisi (seed) yoktur; operatör oranları elle girer.
- Mevcut merchant komisyon modeli (tek bankaya bağlı, sıkı invariant'lı) bu feature ile
  kombinasyon-bazlı modele dönüştürülür; taşınacak üretim verisi yoktur (pre-release).