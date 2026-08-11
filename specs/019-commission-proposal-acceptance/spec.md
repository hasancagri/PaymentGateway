# Feature Specification: Komisyon Teklifi ve Metin-Sürümlü Pazarlık

**Feature Branch**: `019-commission-proposal-acceptance`

**Created**: 2026-08-11

**Status**: Draft (v2 — pazarlık + agent kanalı eklendi; "karşı-teklif yok" varsayımı korunur ama
ret-revize-yeniden teklif döngüsü admin'in METİN kanalıyla döner)

**Input**: User description: "Her yeni merchant için standart komisyon teklifi banka grid'inden
türetilir, Excel ekli mail ile sunulur. Merchant kabul ederse taslak onaylanır, gateway tarafında
insan işi kalmaz. Reddederse gerekçe gelir; admin taslağı METİN üzerinden (satır 37'yi 1.85 yap)
revize eder, değişiklik diff'i kendisine gösterilir, açık 'merchant'a gönder' komutuyla yeni tur
başlar. Oran değişikliği daima insan inisiyatifi; LLM oran üretmez/hesaplamaz."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Metinle teklif sunma (Priority: P1)

Admin, agent'a "Kahve Dünyası'na ilk komisyon teklifimizi sun" yazar; sistem merchant'ı isimden
çözer, standart tarifeyi (banka oranı + sabit marj) taslağa döker ve Excel ekli + kabul/ret linkli
maili merchant'ın iletişim adresine gönderir. Admin hiçbir ekranda hücre doldurmaz.

**Why this priority**: Bugün Finalize merchant'a hiçbir şey iletmiyor; onaysız oran dayatması iş
modeline aykırı. Teklif üretiminin sıfır-ekran olması admin yükünü kaldırır.

**Independent Test**: Agent'a teklif komutu verilir; Mailpit'te Excel ekli + iki linkli mail
görünür; taslak satırları banka oranı + marj değerleriyle eşleşir.

**Acceptance Scenarios**:

1. **Given** banka komisyon grid'i dolu ve merchant'ın iletişim adresi var, **When** admin metinle
   teklif ister, **Then** taslak banka oranı + marj ile oluşur, Excel ekli ve kabul/ret linkli mail
   kuyruğa düşer, teklif "Beklemede" olur, tek-kullanımlık süreli karar bileti üretilir.
2. **Given** banka komisyon grid'i boş, **When** admin teklif ister, **Then** teklif REDdedilir;
   neden (türetilecek kombinasyon yok) bildirilir.
3. **Given** merchant'ın iletişim adresi yok, **When** admin teklif ister, **Then** teklif
   REDdedilir; eksik bildirilir.
4. **Given** bekleyen bir teklif varken, **When** admin yeniden teklif ister, **Then** önceki bilet
   geçersiz olur; yalnız son teklif karar alabilir.

---

### User Story 2 - Merchant kabulü: insansız zincir (Priority: P1)

Merchant maildeki kabul linkine tıklar; teklif Kabul olur, taslak merchant'ın gerçek komisyonu
haline gelir ve aktivasyon koşulu kendiliğinden işler. Gateway tarafında hiçbir insan aksiyonu
gerekmez.

**Why this priority**: Kabul anı sözleşme anıdır; deterministik ve insansız olmalı. Feature'ın
değer vaadi "admin koşmadan anlaşma".

**Independent Test**: Kabul linki tıklanır → teklif Kabul, komisyon hücreleri oluşur, merchant
Active olur; tüm zincir tek tıkla, elle müdahalesiz.

**Acceptance Scenarios**:

1. **Given** bekleyen teklif, **When** merchant kabul linkini kullanır, **Then** teklif Kabul olur,
   taslak satırları merchant komisyonuna kopyalanır ve merchant aktivasyon koşulu (komisyon)
   kendiliğinden sağlanır.
2. **Given** kullanılmış veya süresi dolmuş bilet, **When** kabul linkine gidilir, **Then** işlem
   etkisizdir; durum değişmez, merchant'a "geçersiz bilet" sayfası gösterilir.

---

### User Story 3 - Ret, gerekçe ve metinle revizyon döngüsü (Priority: P1)

Merchant ret linkinden gerekçesini (uzun bir itiraz listesi olabilir) yazar. Admin gerekçeyi görür
ve taslağı metin komutlarıyla revize eder: "satır 37'yi 1.85 yap", "Akbank 6 taksit 1.8",
"tüm 12 taksitleri 0.2 düşür". Her komut sonrası uygulanan değişikliklerin diff'i admin'e geri
gösterilir. Admin dilediği kadar düzenler; merchant'a hiçbir şey gitmez. Açık "merchant'a gönder"
komutuyla yeni teklif turu başlar (yeni Excel, yeni bilet, yeni mail).

**Why this priority**: Pazarlık döngüsü feature'ın özü; düzenleme fazı ile gönderme fazının ayrık
olması kazara gönderimi imkânsız kılar.

**Independent Test**: Ret sonrası satır-no ve banka+taksit adresli komutlarla revize yapılır, diff
doğrulanır, "gönder" ile yeni mail Mailpit'te görünür; gönder denmedikçe mail çıkmaz.

**Acceptance Scenarios**:

1. **Given** bekleyen teklif, **When** merchant ret linkinden gerekçeyle reddeder, **Then** teklif
   Ret olur, gerekçe kaydedilir ve admin'e (agent sorgusu + admin ekranı) görünür.
2. **Given** reddedilmiş teklif, **When** admin "satır 37'yi 1.85 yap" der, **Then** taslağın
   37. satırı (deterministik satır numarası) 1.85 olur ve değişiklik diff'i admin'e gösterilir.
3. **Given** reddedilmiş teklif, **When** admin banka+taksit adresiyle veya toplu işlemle (set /
   delta) değişiklik ister, **Then** hesap sunucu tarafında deterministik yapılır ve diff gösterilir.
4. **Given** taslakta bir satırın yeni değeri banka oranının altında, **When** admin bu değişikliği
   ister, **Then** değişiklik REDdedilir; hangi satırların taban altına indiği bildirilir.
5. **Given** revize edilmiş taslak, **When** admin "merchant'a gönder" der, **Then** yeni teklif
   doğar (yeni bilet + Excel + mail), önceki teklif Geçersiz olur.
6. **Given** revize edilmiş ama gönderilmemiş taslak, **When** admin başka komut vermez, **Then**
   merchant'a hiçbir mail gitmez; taslak çalışma kopyası olarak bekler.

---

### User Story 4 - Kabul sonrası değişmezlik (Priority: P1)

Kabul edilmiş komisyon değiştirilemez; oran revizesi ve yeni teklif denemeleri reddedilir.

**Why this priority**: "Komisyon değiştirilemez" şartı sözleşme güvenidir; kabulün anlamı budur.

**Independent Test**: Kabul sonrası metinle revize ve yeni teklif denenir; ikisi de RET.

**Acceptance Scenarios**:

1. **Given** kabul edilmiş teklif, **When** admin metinle oran değiştirmeye çalışır, **Then** RET edilir.
2. **Given** kabul edilmiş teklif, **When** admin yeni teklif ister, **Then** RET edilir.

---

### User Story 5 - Teklif durumu görünürlüğü (Priority: P2)

Admin, agent sorgusuyla ("Kahve Dünyası teklifi ne durumda?") ve Admin UI komisyon ekranında
teklif durumunu (yok / beklemede / kabul / ret + gerekçe + zaman) görür; taslağın güncel tam
tablosunu agent'tan isteyebilir.

**Why this priority**: Ret gerekçesi görülemezse revizyon döngüsü işlemez; "taslağı göster"
olmadan uzun düzenlemenin son kontrolü yapılamaz.

**Independent Test**: Ret sonrası agent sorgusu "Ret + gerekçe" döner; "taslağı göster" satır
numaralı güncel tabloyu döner; Admin UI aynı durumu gösterir.

**Acceptance Scenarios**:

1. **Given** reddedilmiş teklif, **When** admin durumu sorar, **Then** ret durumu + gerekçe + zaman
   görünür.
2. **Given** düzenlenmiş taslak, **When** admin taslağı ister, **Then** satır numaralı tam tablo
   (banka, taksit, oran) döner.

---

### Edge Cases

- Karar biletinin TTL'i dolarsa: linkler işlem yapmaz; admin yeniden gönderir (yeni bilet, eski ölür).
- Geçersiz satır numarası ("satır 999") veya bilinmeyen banka/taksit adresi: değişiklik REDdedilir,
  geçerli aralık bildirilir; taslak değişmez.
- Toplu işlem (delta) bazı satırları taban altına indirirse: işlem BÜTÜN olarak reddedilir, ihlal
  eden satırlar listelenir (kısmi uygulama yok).
- Teklif maili ulaşmazsa: admin "gönder" ile yeniden gönderir (yeni bilet, yeni mail).
- Banka grid'i teklif sonrası değişirse: gönderilmiş teklif fotoğraf olduğundan etkilenmez; taban
  bekçisi her revizede güncel banka oranına bakar.
- Merchant Active olduktan sonra gelen (yarış) karar tıklaması: bilet zaten kullanılmış → etkisiz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Standart tarife ayrı tablo/ekran olmadan, teklif anında banka komisyon grid'inden
  türetilmeli: her kombinasyon için banka oranı + yapılandırılabilir sabit marj (ekransız config).
- **FR-002**: Admin teklif sürecini yalnız metin (agent) kanalıyla yürütebilmeli: teklif sunma,
  taslak revize, taslağı görme, durum sorma. Bu işlemler için ekran zorunluluğu olmamalı.
- **FR-003**: Teklif, taslağın tablo halini (Excel eki) ve kabul / gerekçeli-ret linklerini içeren
  mail ile merchant iletişim adresine gitmeli; mail gövdesinde kısa özet bulunmalı.
- **FR-004**: Karar linkleri kimlik doğrulaması istememeli; yetki biletin kendisidir
  (tek kullanım + TTL + yalnız son teklif geçerli).
- **FR-005**: Kabul tamamen insansız işlemeli: taslak satırları merchant komisyonuna kopyalanır,
  mevcut aktivasyon koşul zinciri (komisyon hazır → Active) kendiliğinden tetiklenir.
- **FR-006**: Ret, gerekçe metnini kaydetmeli; gerekçe admin'e agent sorgusunda ve admin ekranında
  görünmeli.
- **FR-007**: Taslak revizyonu yalnız admin'in AÇIK değerlerini taşımalı; adresleme deterministik
  satır numarası VEYA banka+taksit ile yapılabilmeli; toplu işlemler (set / delta) sunucu tarafında
  deterministik hesaplanmalı. LLM oran üretmez, hesap yapmaz — yalnız komutu yapılandırılmış
  çağrıya çevirir.
- **FR-008**: Her revizyon komutu sonrası uygulanan değişikliklerin diff'i (eski → yeni) admin'e
  geri gösterilmeli.
- **FR-009**: Banka oranının altına inen her revizyon (tekil veya toplu) bütün olarak reddedilmeli;
  ihlal eden satırlar bildirilmelidir (taban bekçisi).
- **FR-010**: Düzenleme fazı ile gönderme fazı ayrık olmalı: revizyonlar taslağı anında değiştirir
  ama merchant'a hiçbir şey gitmez; mail yalnız açık "merchant'a gönder" komutuyla çıkar.
- **FR-011**: "Gönder", taslağın fotoğrafını yeni teklif yapar: yeni bilet + yeni Excel + yeni mail;
  önceki bekleyen teklif Geçersiz olur. Aynı anda tek karar alabilir teklif bulunur.
- **FR-012**: Kabul sonrası oran değişikliği ve yeni teklif reddedilmeli (değişmezlik).
- **FR-013**: Mevcut manuel Finalize akışı ve Draft/Ready ayrımı kalkmalı; komisyonun "hazır"
  olmasının tek yolu merchant kabulü olmalı. Admin komisyon ekranı salt-okuma kalır ve teklif
  durumunu gösterir.
- **FR-014**: Excel üretimi deterministik altyapı işidir; teklif tablosu satır numaralı ve sıralaması
  deterministik olmalı (satır-no adreslemenin temeli). Agent/LLM Excel üretiminde yer almaz.

### Key Entities

- **Komisyon Taslağı (draft)**: Merchant başına çalışma kopyası; deterministik sıralı, satır
  numaralı (banka, taksit, oran) satırları. İlk teklifte banka grid'i + marjdan doğar; ret sonrası
  metinle revize edilir; "gönder" ile fotoğrafı teklife dönüşür.
- **Komisyon Teklifi (CommissionProposal)**: Gönderilmiş taslak fotoğrafı; durum
  (Beklemede/Kabul/Ret/Geçersiz), tek-kullanımlık + TTL'li karar bileti, karar zamanı, ret
  gerekçesi. Merchant başına yalnız biri karar alabilir (son gönderilen).
- **Komisyon hücreleri (MerchantCommission)**: Mevcut yapı; artık yalnız kabul anında taslaktan
  kopyalanarak oluşur, kabul sonrası kilitli.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, hiçbir ekran kullanmadan tek metin komutuyla teklif sunar; mail (Excel + 2
  link) teslim kuyruğuna 5 sn içinde düşer.
- **SC-002**: Kabul tek tıktır; gateway tarafında sıfır insan aksiyonuyla merchant Active olur.
- **SC-003**: Revizyon komutlarının %100'ü diff yankısıyla yanıtlanır; taban altına inen
  denemelerin %100'ü reddedilir.
- **SC-004**: "Gönder" denmedikçe merchant'a giden mail sayısı 0'dır; kullanılmış/süresi dolmuş
  biletle yapılan denemelerin %100'ü etkisizdir.
- **SC-005**: Kabul sonrası oran değiştirme denemelerinin %100'ü reddedilir.
- **SC-006**: Ret gerekçesi kayıpsız görünür (agent sorgusu + admin ekranı).

## Assumptions

- Karşı-teklif yok: merchant oran GİRMEZ; ya kabul eder ya gerekçeyle reddeder. Pazarlık, admin'in
  metinle revize + yeniden gönder döngüsüdür. (A2A merchant-agent pazarlığı ayrı aday.)
- Inbound mail okuma/parse ve Excel upload kapsam DIŞI; merchant'tan yapılandırılmış girdi yok.
- Admin metin kanalı Merchant.Agent'tır (yeni proje açılmaz); komisyon işlemleri için Commission
  tarafına yeni agent yüzeyi açılır. Oran değişikliği daima insan inisiyatifi.
- Excel her iki yönde de yalnız taşıma/görüntü formatıdır ve sistemce üretilir; üretim mail
  altyapısında yapılır (mesaj generic tablo taşır). Excel.Mcp bu deterministik akışta kullanılmaz
  (MCP yalnız agent yüzeyi kuralı).
- Mail teslimi mevcut mail kuyruğu/worker'ıyla olur; mesaj sözleşmesine generic tablo eki eklenir.
- Bilet deseni (tek kullanım + TTL + son-teklif-geçerli) merchant aktivasyon biletiyle aynı
  kurallardadır. Karar sayfaları merchant'a dönük minimal onay/gerekçe sayfalarıdır.
- Marj tek global değerdir (config); merchant-özel marj ileriki iş.
- Banka grid'i (tabanlar) gateway-otoriterdir; teklif yalnız merchant tarifesi içindir.