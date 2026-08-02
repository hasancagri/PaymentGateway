# Feature Specification: BinCard Katalog Görüntüleme Ekranları (Admin)

**Feature Branch**: `009-bincard-admin-ui`

**Created**: 2026-08-02

**Status**: Draft

**Input**: User description: "009 BinCard Katalog Admin UI — Payment.Api'deki BinCard kataloğunu (~9957 kayıt) Admin Razor Pages BFF'te salt-okunur göster. Mevcut Admin desenine sadık (Banks/Merchants gibi typed HttpClient + service discovery ile payment-api'yi tüket, backend'e kural sızdırma, Türkçe MessageText). Kullanıcı bir BIN girip tekil çözümü görebilmeli; ayrıca katalogu sayfalı listeleyip banka kodu / kart programı ile filtreleyebilmeli. 9957 kayıt olduğu için düz tam liste YOK — arama + sayfalama zorunlu. Kayıt-başı elle düzenleme/CRUD YOK. Yetki yok."

## User Scenarios & Testing *(mandatory)*

Aktör: **Gateway admin** (ödeme gateway'ini işleten platform-tarafı operatör; merchant değil). Bu
Admin paneli iç bir yönetim aracıdır. Amaç: operatörün Payment BC'ye taşınan BIN kataloğunu (008 ile
DB'ye alınmış ~9957 kayıt) gözle denetleyebilmesi — şu an yalnız API (curl/Scalar) ya da doğrudan DB
sorgusuyla mümkün. Ekranlar **salt-okuma**: katalog güncelleme yalnız mevcut import API'siyle yapılır.
Aşağıda "operatör" = bu gateway admin.

### User Story 1 - Bir BIN'i çöz ve kart bilgisini gör (Priority: P1) 🎯 MVP

Operatör bir arama kutusuna BIN numarası (6 ya da 8 hane) girer ve o BIN'in katalog çözümünü tek bir
detay görünümünde görür: banka kodu, kart tipi (banka/kredi), kart markası, kart programı, ticari kart
mı, ve taksit yapılabilen banka kodları listesi. BIN katalogda yoksa "bulunamadı" bilgisi gösterilir.

**Why this priority**: Operatörün en sık ihtiyacı "şu kart hangi banka/program, taksit hangi bankalara
yapılıyor" sorusudur; tekil çözüm bunu tek başına karşılar. Bağımsız teslim edilince operatör API'ye
gitmeden bir BIN'in ödeme yolunda nasıl davranacağını (008 çözümü) doğrulayabilir.

**Independent Test**: Bilinen bir kredi BIN'i (ör. 365770) girilir → detay banka/tip/marka/program/
ticari + taksit-banka listesini doğru gösterir; bir banka-kartı BIN'i girilir → taksit-banka listesi
boş; var olmayan BIN (ör. 999999) girilir → "bulunamadı"; 8 haneli giriş → ilk 6 ile çözülür.

**Acceptance Scenarios**:

1. **Given** katalogda kredi kartı olan bir BIN, **When** operatör BIN'i arar, **Then** detay görünümü
   banka kodu, "Kredi", marka, program, ticari bayrağı ve taksit yapılabilen banka kodlarını gösterir.
2. **Given** katalogda banka kartı (debit) olan bir BIN, **When** operatör arar, **Then** kart tipi
   "Banka" görünür ve taksit-banka listesi boştur (uygun bilgi mesajıyla).
3. **Given** katalogda olmayan bir BIN, **When** operatör arar, **Then** ekran "bu BIN katalogda yok"
   bilgisini gösterir (hata/çökme değil).
4. **Given** 8 haneli bir giriş, **When** tam eşleşme yoksa, **Then** ilk 6 haneyle çözüm denenir ve
   sonucu gösterilir.
5. **Given** geçersiz giriş (boş / rakam-dışı / 6 haneden kısa), **When** operatör arar, **Then**
   Türkçe bir doğrulama mesajı gösterilir, sorgu yapılmaz.

---

### User Story 2 - Katalogu sayfalı listele ve filtrele (Priority: P2)

Operatör katalog kayıtlarını bir tabloda sayfa sayfa gezer: her satır BIN, banka kodu, kart tipi,
marka, program, ticari bayrağı gösterir. Operatör bu alanların herhangi biriyle (banka kodu, kart
programı, kart tipi, kart markası, ticari bayrağı) filtreler; birden çok filtre birlikte uygulanınca
kesişim döner. Kayıt sayısı büyük (~9957) olduğundan liste **her zaman sayfalıdır** ve filtresiz tam
döküm sunulmaz.

**Why this priority**: Belirli bir BIN bilinmediğinde (ör. "0062 bankasının Bonus kartları hangileri")
kataloğu tarama ihtiyacı doğar. Tekil çözümden (US1) sonra gelir çünkü çözüm tekil sorgudan daha sık;
tarama ikincil denetim aracıdır.

**Independent Test**: Filtre olarak bir banka kodu seçilir → tablo yalnız o bankanın kayıtlarını
sayfalı gösterir; sayfa ileri/geri çalışır; kart programı/tipi/markası/ticari filtreleri eklenince
kesişim daralır; filtre sonucu boşsa "kayıt yok" gösterilir.

**Acceptance Scenarios**:

1. **Given** dolu katalog, **When** operatör liste ekranını açar, **Then** ilk sayfa (sabit sayıda
   kayıt) toplam kayıt/sayfa bilgisiyle birlikte gösterilir.
2. **Given** liste ekranı, **When** operatör bir banka kodu filtresi uygular, **Then** yalnız o banka
   kodunun kayıtları sayfalı listelenir.
3. **Given** banka kodu filtresi uygulanmış liste, **When** operatör ayrıca bir kart programı seçer,
   **Then** iki filtrenin kesişimi (o banka + o program) listelenir.
4. **Given** liste ekranı, **When** operatör kart tipi (Kredi), kart markası (ör. Troy) veya ticari
   bayrağı (ör. yalnız ticari) filtresi uygular, **Then** yalnız o niteliğe uyan kayıtlar sayfalanır;
   bu filtreler diğerleriyle birlikte kesişim olarak çalışır.
5. **Given** hiçbir kayda uymayan filtre kombinasyonu, **When** liste yenilenir, **Then** "sonuç yok"
   bilgisi gösterilir (boş liste hata değildir).
6. **Given** çok sayfalı sonuç, **When** operatör sonraki/önceki sayfaya geçer, **Then** doğru kayıt
   dilimi ve sayfa göstergesi görünür.

---

### Edge Cases

- **Payment API erişilemez**: ekran, backend'den sonuç alamazsa Türkçe bir hata mesajı gösterir; panel
  çökmez (mevcut Admin BFF davranışıyla tutarlı).
- **Boş katalog** (seed öncesi): US1 "bulunamadı", US2 "kayıt yok" gösterir.
- **Çok kısa/çok uzun BIN girişi**: US1 doğrulama mesajıyla reddeder; 8'den uzun giriş de reddedilir
  (gerçek 8-hane çözümleme 008 kapsamında yok).
- **Bilinmeyen marka/program değeri**: katalogda "Unknown" olarak durabilir; ekran bunu okunur bir
  etiketle (ör. "Bilinmiyor") gösterir, boş/çökme değil.
- **Büyük filtre sonucu** (ör. tek banka binlerce kayıt): sayfalama sınırı korunur; tek seferde tüm
  döküm çekilmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admin paneli, operatörün bir BIN numarası (6 veya 8 hane) girip tekil katalog çözümünü
  görebileceği bir arama/detay ekranı sağlamalı.
- **FR-002**: Tekil çözüm görünümü şu alanları göstermeli: banka kodu, kart tipi (Banka/Kredi), kart
  markası, kart programı, ticari kart bayrağı, taksit yapılabilen banka kodları listesi.
- **FR-003**: BIN katalogda bulunamazsa ekran, hata/çökme yerine anlaşılır bir "bulunamadı" bilgisi
  göstermeli.
- **FR-004**: 8 haneli girişte tam eşleşme yoksa çözüm ilk 6 haneyle denenmeli (008 çözüm davranışıyla
  aynı sonucu yansıtır).
- **FR-005**: Geçersiz BIN girişi (boş, rakam-dışı, 6 haneden kısa, 8'den uzun) sorgu yapılmadan Türkçe
  bir doğrulama mesajıyla reddedilmeli.
- **FR-006**: Admin paneli, katalog kayıtlarını **sayfalı** bir tablo olarak listeleyebilmeli; her
  satır BIN, banka kodu, kart tipi, marka, program ve ticari bayrağını göstermeli.
- **FR-007**: Liste şu alanların herhangi biriyle filtrelenebilmeli: banka kodu, kart programı, kart
  tipi (Banka/Kredi), kart markası, ticari bayrağı. Birden çok filtre birlikte uygulanınca kesişim
  sonucunu vermeli; hiçbiri seçilmezse (sayfalı) tüm katalog listelenir.
- **FR-008**: Liste her zaman sayfalı olmalı; filtresiz tam katalog dökümü (~9957 kayıt) tek sayfada
  sunulmamalı. Sayfa boyutu sabit/sınırlı olmalı ve sayfa ileri/geri gezinmesi çalışmalı.
- **FR-009**: Ekranlar **salt-okuma** olmalı — kayıt ekleme/düzenleme/silme (per-kayıt CRUD) sunmamalı.
  Katalog güncelleme yalnız mevcut import API'siyle yapılır ve bu feature'ın kapsamı dışındadır.
- **FR-010**: Ekranlar tüm kullanıcı-yüzü metinlerini (etiketler, mesajlar, enum değerleri) **Türkçe**
  ve okunur biçimde göstermeli (kart tipi/marka/program teknik enum yerine anlaşılır etiket).
- **FR-011**: Admin paneli, katalog verisini yalnız Payment BC'nin sunduğu API sonucundan göstermeli;
  çözüm/türetme kuralını (banka türetme, 8→6 vb.) kendisi yeniden uygulamamalı (backend'e kural
  sızdırma yok).
- **FR-012**: Backend erişilemez veya hata dönerse ekran, paneli çökertmeden Türkçe bir hata mesajı
  göstermeli.

### Key Entities *(include if feature involves data)*

- **BIN Katalog Kaydı** (görüntülenen): bir BIN numarasının katalogtaki kart/banka nitelikleri —
  BIN, banka kodu, kart tipi, kart markası, kart programı, ticari bayrağı. (Sahibi Payment BC; Admin
  yalnız gösterir.)
- **BIN Çözümü** (görüntülenen): bir BIN için türetilmiş kart bilgisi — banka kodu, kredi/banka,
  taksit yapılabilen banka kodları listesi. (Payment BC üretir; Admin yalnız gösterir.)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operatör, bir BIN numarasını arayıp tekil çözümünü (banka/tip/marka/program/ticari/
  taksit-banka) tek bir ekranda, API aracı kullanmadan görebilir.
- **SC-002**: Operatör, kataloğu banka kodu, kart programı, kart tipi, kart markası ve/veya ticari
  bayrağına göre (birlikte kesişim) filtreleyip sonuçları sayfa sayfa gezebilir; hiçbir görünümde tek
  seferde ~9957 kaydın tamamı yüklenmez.
- **SC-003**: Bilinen bir BIN için ekranda gösterilen kart bilgisi, 008 çözüm sonucuyla (banka/tip/
  marka/program/ticari + taksit-banka listesi) birebir aynıdır.
- **SC-004**: Bilinmeyen BIN, geçersiz giriş, boş filtre sonucu ve backend erişilemez durumlarının
  hepsinde panel çökmeden anlaşılır Türkçe bilgi/hata mesajı gösterir.

## Assumptions

- **Banka kodu ham gösterilir**: Katalog yalnız 4-haneli banka kodunu (string) tutar; banka **adı**
  ayrı bir kaynaktan (Commission BC) çözülmez — kapsam dışı. Ekran banka kodunu olduğu gibi gösterir.
- **Liste satırında taksit-banka türetme yok**: Taksit yapılabilen banka listesi yalnız tekil çözüm
  (US1) görünümünde gösterilir; sayfalı liste (US2) yalnız ham katalog alanlarını gösterir (satır-başı
  türetme maliyetli ve gereksiz).
- **Yeni backend uçları gerekebilir**: Payment.Api şu an yalnız tekil `GET {bin}` (çözüm) ve `import`
  sunar; sayfalı liste + banka/program filtresi için Payment BC tarafına yeni bir sorgu ucu eklenmesi
  bu feature kapsamındadır (Admin bu ucu tüketir). Tekil çözüm mevcut ucu tüketir.
- **Yetkilendirme yok**: Proje-geneli AUTHZ ertelemesi gereği ekranlar ve tükettiği uçlar korumasız
  (Identity BC ile gelecek). Admin paneli zaten yetkisiz iç araçtır.
- **Mevcut Admin BFF deseni**: Ekranlar Banks/Merchants/SettlementAccounts ekranlarının Razor Pages +
  typed HttpClient + service discovery desenini izler; kural içermez, yalnız API sonucunu gösterir.
- **Sayfa boyutu**: Makul sabit bir sayfa boyutu (ör. 20-50 kayıt) yeterlidir; kesin değer plan/tasarım
  aşamasında belirlenir.