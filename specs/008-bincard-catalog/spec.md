# Feature Specification: BinCard Referans Kataloğu — BIN Verisini Domain DB'sine Taşıma

**Feature Branch**: `008-bincard-catalog`

**Created**: 2026-08-02

**Status**: Draft

**Input**: User description: "CP.VPOS'taki gömülü BIN verisini (~9900 kayıt, 6 haneli binNumber) Payment BC veritabanına referans lookup katalog olarak taşı; BIN verisini donmuş kütüphaneden çıkarıp güncellenebilir kıl ve BIN okuma yolunu domain DB'ye çevir. Onaylı brainstorm 2026-08-02."

## Bağlam ve Kapsam

Bugün kart BIN'inden (kartın ilk 6 hanesi) banka + kart tipi/markası/programı çözümü, donmuş
`CP.VPOS` kütüphanesinin içine **gömülü** bir BIN tablosundan yapılıyor. Bu tablo kod içinde
sabittir; güncellemek için kütüphaneyi yeniden derlemek gerekir ve BIN çözümü bu legacy kütüphaneye
bağımlıdır.

Bu feature, BIN verisini **Payment bounded context'inin kendi veritabanına** bir **referans lookup
kataloğu** olarak taşır. Böylece (a) veri kütüphaneden bağımsız, yayınlanan yeni BIN listeleriyle
**güncellenebilir** olur ve (b) ödeme/taksit akışları BIN'i **domain DB'sinden** çözer, donmuş
kütüphaneyi BIN için çağırmaz.

Veri profili **referans/lookup**: okuma çok sık (her ödeme/quote), yazma nadir ve toplu (banka yeni
BIN aralığı yayınladığında). Bu yüzden katalog davranış-zengin bir aggregate değil; sorgu-optimize
bir okuma modeli + toplu import mekanizmasıdır.

**Kapsam sınırı (bilinçli kapsam dışı):**

- **Gerçek 8-haneli BIN veri desteği**: Türkiye 8 haneli BIN'e geçti ama mevcut veri hâlâ 6 hane.
  Bu sürüm 6 haneyi taşır; 8-haneli girdi ilk 6'ya düşürülerek çözülür (mevcut davranış korunur).
  Gerçek 8-haneli veri ayrı bir iş.
- **Uluslararası BIN alanları** (ülke/bölge/euro-bölge/uluslararası üye kimliği): sistem yalnız TL
  ve yurt-içi çalıştığından kapsam dışı (YAGNI).
- **Admin UI / kayıt-başı elle düzenleme**: ~9900 kayıt elle yönetilmez; güncelleme yalnız toplu
  import iledir.
- **Çağıranın bilinmeyen-BIN politikası**: "BIN bulunamazsa akış ne yapsın" (reddet / peşine düş /
  3D zorla) çağıran akışın kararıdır; bu feature yalnız kataloğun temiz "bulunamadı" dönmesini kapsar.

Bu feature **007 (A2A ödeme oturumu)** BIN çözümünü besleyebilir ama ondan bağımsızdır; biri
diğerini bloklamaz.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - BIN'den kart bilgisini katalogdan çöz (Priority: P1)

Ödeme/taksit akışı, bir kartın ilk 6 (veya 8) hanesini verir; sistem BIN'i **domain kataloğundan**
çözer ve bankayı + kart tipini (kredi/banka) + kart programını + ticari-kart bilgisini döner.
Ayrıca o kart programını destekleyen bankaların kod listesini (taksit yapabilen bankalar) türetir.
Çözüm donmuş kütüphaneye değil, DB kataloğuna dayanır.

**Why this priority**: Feature'ın asıl değeri budur — BIN çözümü artık güncellenebilir domain
verisinden gelir. Diğer akışların (routing, taksit) tükettiği çekirdek yetenek. Tek başına test
edilebilir: bir BIN ver, dönen kart bilgisini doğrula.

**Independent Test**: Katalog dolu iken bilinen bir BIN ile çözüm istenir; dönen banka/kart-tipi/
program/ticari alanları ve türetilen taksit-banka listesi, legacy sonuçla **aynı** olacak şekilde
doğrulanır. 8-haneli girdi ilk 6'ya düşülerek çözülür.

**Acceptance Scenarios**:

1. **Given** katalogda bulunan 6 haneli bir BIN, **When** çözüm istenir, **Then** bankası, kart
   tipi, kart markası, kart programı ve ticari-kart bilgisi döner.
2. **Given** kredi kartı ve geçerli bir kart programı olan bir BIN, **When** çözüm istenir, **Then**
   o programı destekleyen bankaların kod listesi (taksit yapabilen bankalar) türetilerek döner;
   kartın kendi bankası listenin başındadır.
3. **Given** 8 haneli bir girdi, **When** çözüm istenir, **Then** kayıt önce 8 hane ile aranır,
   bulunamazsa ilk 6 haneyle çözülür.
4. **Given** katalogda olmayan bir BIN, **When** çözüm istenir, **Then** "bulunamadı" anlamlı sonucu
   döner — istisna fırlatılmaz, kart verisi sızmaz; çağıran akış sonucu kendi politikasına göre ele alır.

---

### User Story 2 - Katalogu ilk kez doldur (seed) (Priority: P1)

Sistem ilk ayağa kalktığında BIN kataloğu boşsa, mevcut yerleşik BIN kaynağından (~9900 kayıt) bir
kez otomatik doldurulur. Böylece Story 1 çözümü veri bulur.

**Why this priority**: Katalog boşken çözüm değer üretmez; seed, Story 1'in ön koşuludur. P1.

**Independent Test**: Boş katalog ile sistem başlatılır; seed sonrası kayıt sayısı beklenen mertebede
(~9900) ve örnek bir bilinen BIN çözülebilir olur. İkinci başlatmada tekrar seed edilmez (kayıt
çoğalmaz).

**Acceptance Scenarios**:

1. **Given** boş bir BIN kataloğu, **When** sistem başlatılır, **Then** yerleşik kaynaktaki tüm BIN
   kayıtları katalogda oluşur.
2. **Given** zaten dolu bir katalog, **When** sistem yeniden başlatılır, **Then** yeniden seed
   yapılmaz; kayıtlar çoğalmaz/bozulmaz.

---

### User Story 3 - Yayınlanan BIN listesiyle güncelle (idempotent toplu import) (Priority: P2)

Bir operatör, yeni yayınlanan bir BIN listesini (banka yeni aralık ekledi/değiştirdi) sisteme
toplu olarak yükler. Sistem listeyi mevcut katalogla birleştirir (upsert): var olan BIN'ler
güncellenir, yeni BIN'ler eklenir. İşlem idempotenttir — aynı liste iki kez yüklenirse sonuç
değişmez. Bunun için yeniden derleme/deploy gerekmez.

**Why this priority**: Feature'ın "güncellenebilirlik" vaadini bu tamamlar; ama çekirdek çözüm+seed
olmadan tek başına anlamsız. P2.

**Independent Test**: Bilinen bir alt küme içeren bir liste yüklenir; değişen kayıtların güncellendiği,
yeni kayıtların eklendiği, tekrar yükleyince kayıt sayısının ve içeriğin **değişmediği** doğrulanır.

**Acceptance Scenarios**:

1. **Given** dolu bir katalog ve içinde bazıları var olan bazıları yeni BIN'ler taşıyan bir liste,
   **When** toplu import çalıştırılır, **Then** var olanlar güncellenir, yeni olanlar eklenir.
2. **Given** aynı listenin ikinci kez yüklenmesi, **When** import tekrar çalıştırılır, **Then**
   katalog içeriği ve kayıt sayısı değişmez (idempotency).
3. **Given** listede geçersiz/eksik alanlı bir kayıt, **When** import çalıştırılır, **Then** o kayıt
   anlamlı biçimde atlanır/raporlanır; geçerli kayıtların yüklenmesi engellenmez.

---

### Edge Cases

- **Bilinmeyen BIN** (katalogda yok, muhtemelen yabancı/yeni kart): katalog **null / "bulunamadı"**
  döner — istisna yok, **sahte/boş bir kart bilgisi de üretilmez**. Çağıran akış null'ı bilinçli ele
  alır. (Bilinçli değişiklik: bugünkü kod bilinmeyen BIN'de banka-kodu-boş / kredi-değil / taksit-yok
  bir kart bilgisine sessizce degrade ediyor; yeni davranış yanıltıcı boş nesne yerine açık null döner.)
- **8 haneli girdi, 6 haneli kayıt**: 8→ilk6 düşürme ile çözülür.
- **Boş katalog** (seed öncesi / seed başarısız): çözüm "bulunamadı" döner; sistem çökmez.
- **Tekrarlı import / tekrarlı seed**: idempotent — kayıt çoğalmaz.
- **Geçersiz kayıt import'ta**: atlanır/raporlanır, batch'i bozmaz.
- **Kart programı = bilinmeyen veya banka kartı**: taksit-banka listesi türetilmez/boş kalır.

## Requirements *(mandatory)*

### Functional Requirements

**Katalog ve veri**

- **FR-001**: Sistem, BIN kayıtlarını Payment bounded context'inin kendi kalıcı deposunda bir
  referans katalog olarak tutmalı; her kayıt en az BIN numarası, banka kodu, kart tipi (kredi/banka),
  kart markası, kart programı ve ticari-kart bilgisini taşımalı.
- **FR-002**: Kart tipi, marka ve program alanları tip-güvenli değerlerle (tanımlı sabit kümesi)
  temsil edilmeli; ham sayısal legacy kodları katalog sınırına çevrilerek girmeli, domain bu ham
  kodlara bağlı kalmamalı (legacy kütüphane tipleri domain sınırını geçmez).
- **FR-003**: BIN numarası ile kayıt **birebir** (exact-match) bulunabilmeli; katalog bu erişim için
  optimize olmalı.

**Çözümleme (Story 1)**

- **FR-004**: Sistem, verilen bir BIN'i (6 hane) katalogdan çözüp banka + kart tipi + marka + program
  + ticari bilgisini dönmeli.
- **FR-005**: Sistem, 8 haneli bir girdide önce tam eşleşme aramalı; bulunamazsa ilk 6 hane ile
  çözmeli (mevcut davranış korunur).
- **FR-006**: Sistem, kredi kartı ve geçerli program için o programı destekleyen bankaların kod
  listesini (taksit yapabilen bankalar) çözüm anında türetmeli; kartın kendi bankası bu listenin
  başında olmalı. Banka kartı veya bilinmeyen program için liste boş/uygulanmaz.
- **FR-007**: Katalogda olmayan BIN için sistem **null / "bulunamadı"** dönmeli; istisna fırlatmamalı,
  **sahte/varsayılan bir kart bilgisi üretmemeli** ve kart verisi sızdırmamalı. Çağıran null'ı
  kendisi ele alır. (Beklenen hata Result/null ile taşınır — anayasa IV.)

**Seed ve güncelleme (Story 2 & 3)**

- **FR-008**: Katalog boşsa, sistem mevcut yerleşik BIN kaynağından bir kez otomatik doldurulmalı
  (seed). Katalog doluysa yeniden seed edilmemeli.
- **FR-009**: Sistem, yayınlanan bir BIN listesini toplu olarak içe aktarabilmeli; işlem **idempotent
  upsert** olmalı (var olan güncellenir, yeni eklenir, tekrar yükleme içeriği değiştirmez).
- **FR-010**: Toplu import, geçersiz/eksik alanlı kayıtları anlamlı biçimde atlamalı/raporlamalı;
  geçerli kayıtların yüklenmesini engellememeli.
- **FR-011**: Güncelleme yeniden derleme/deploy gerektirmemeli; katalog canlı güncellenebilir olmalı.

**Okuma yolu ve sınırlar**

- **FR-012**: Payment context'inin BIN okuması (bugün donmuş kütüphaneye giden) bu katalog çözümüne
  geçmeli; donmuş kütüphane BIN için artık çağrılmamalı. Donmuş kütüphanenin kendisi değiştirilmemeli.
- **FR-013**: Sistem yalnız yurt-içi/TL kapsamı desteklemeli; uluslararası BIN alanları modellenmemeli.

### Key Entities *(include if feature involves data)*

- **BinCard (referans kayıt)**: Bir BIN numarasının kart/banka nitelikleri. Nitelikler: BIN numarası
  (6 hane, erişim anahtarı), banka kodu, kart tipi (kredi/banka), kart markası (Visa/MasterCard/Troy/
  Amex/… veya bilinmeyen), kart programı (Bonus/World/Maximum/Axess/… veya bilinmeyen), ticari-kart
  (evet/hayır). Davranış-zengin değil, referans/lookup verisi.
- **Kart Bilgisi (çözüm çıktısı)**: Çözümün çağırana döndürdüğü temsil: banka kodu, kredi/banka,
  program ve taksit yapabilen bankaların kod listesi (türetilir). Mevcut ödeme/taksit akışlarının
  tükettiği biçim.
- **BIN listesi (import girdisi)**: Yayınlanan, toplu upsert edilecek BIN kayıtları kümesi.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Bilinen BIN'ler için katalog çözümü, legacy (gömülü kaynak) çözümüyle **birebir aynı**
  banka/tip/marka/program/ticari sonucu üretir (parite; örneklem üzerinde sapma 0).
- **SC-002**: BIN verisi, donmuş kütüphaneyi yeniden derlemeden güncellenebilir — bir güncelleme
  toplu import ile uygulanabilir ve çözüm sonucu buna göre değişir.
- **SC-003**: Seed sonrası katalog, yerleşik kaynaktaki tüm kayıtları (~9900) içerir; ikinci
  başlatma kayıt sayısını değiştirmez (seed idempotent).
- **SC-004**: Aynı BIN listesinin iki kez import'u katalog içeriğini ve kayıt sayısını değiştirmez
  (import idempotent).
- **SC-005**: Bilinmeyen BIN çözümü hiçbir durumda istisna/çökme üretmez ve kart verisi sızdırmaz.

## Assumptions

- **Model & yerleşim**: BinCard, Payment bounded context'inin kendi deposunda yaşar (BIN çözümü her
  ödeme/quote'ta hot-path; ayrı context senkron cross-context bağımlılığı gerektirirdi). Mevcut
  "yerel banka-kodu referansı" desenine uyumludur.
- **Seed kaynağı**: İlk doldurma, mevcut yerleşik BIN kaynağının (bugünkü gömülü tablonun) dışa açık
  okuma yüzeyinden yapılır; donmuş kütüphane değiştirilmez.
- **Bilinmeyen BIN davranışı (bilinçli değişiklik)**: Bugünkü kod bilinmeyen BIN'de sahte-boş bir
  kart bilgisine degrade ediyor. Yeni davranış: katalog **null / "bulunamadı"** döner, sahte nesne
  üretmez; çağıran null'ı bilinçli ele alır. "Reddet mi peşine mi düş / 3D zorla" politikası
  çağıranındır. (Bu, bilinen-BIN paritesini — SC-001 — etkilemez.)
- **8 hane**: Mevcut veri 6 hanelidir; 8-haneli çözüm ilk-6 düşürme ile idare edilir. Gerçek 8-haneli
  veri ayrı iş.
- **Uluslararası alanlar & legacy zengin model**: Kapsam dışı (yalnız TL/yurt-içi; YAGNI).
- **Test**: Proje kuralı gereği saf domain birim testleri (ham kod→tip-güvenli değer eşlemesi, 8→6
  düşürme, taksit-banka türetme, import idempotency). Dış/HTTP çağrıları birim testi edilmez;
  quickstart ile elle doğrulanır.
- **Yetki**: Proje-geneli erteleme (Identity BC ile gelecek); import/çözüm uçları şimdilik korumasız.
- **Para birimi**: Yalnız TL.

## Dependencies

- **Yerleşik BIN kaynağı**: İlk seed, bugünkü gömülü BIN tablosunun dışa açık okuma yüzeyini tüketir;
  bu feature o kaynağı **değiştirmez**, yalnız okur ve domain kataloğuna kopyalar.
- **Tüketiciler**: Mevcut routing/taksit akışları (ve ileride 007 A2A oturumu) BIN çözümünü bu
  katalogtan alacak biçimde geçirilir.