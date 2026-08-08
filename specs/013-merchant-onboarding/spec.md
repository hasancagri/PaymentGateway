# Feature Specification: Merchant Onboarding — Agentic Kayıt + İnsan Onayı + Kademeli Yetki

**Feature Branch**: `013-merchant-onboarding`

**Created**: 2026-08-07

**Status**: Draft

**Input**: User description: "Merchant Onboarding — agentic kayıt + insan onayı + kademeli yetki (013). Merchant adayı A2A üzerinden başvurur (yeni Merchant.Agent: A2A host + LLM router + MCP client → Merchant.Api + Commission.Api; Payment.Agent şablonu). Gateway aday sitesinden /.well-known/merchant-descriptor.json çeker ve HTTP-01 tarzı domain-control challenge doğrular. Geçerse başvuru RegisterRequest olarak kaydedilir (merchant HENÜZ oluşmaz) ve admin'e MCP mail gider. Admin, Admin UI 'Merchant Talepleri' sayfasından inceler; onayla merchant oluşur (key o anda üretilir) ve aktivasyon maili gider; aktivasyon sayfası Identity.Server'da, MerchantKey orada teslim edilir. Key teslimiyle Provisioning: sınırlı scope demeti; charge kapalı. Komisyon ONAY SONRASI belirlenir: admin grid'i tanımlayınca merchant'a 'şartların hazır' maili; agent A2A ile sorar/kabul eder/reddeder (grid yoksa 'hazırlanıyor', fail-closed). Provisioning→Active OTOMATİK: settlement hesabı + komisyon kabulü + ReturnUrl. externalRef opak alan. Kapsam dışı: RBAC, kart vault/charge (G5), DB-per-tenant (G4), ECommerce repo işleri (E1)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant adayı agent aracılığıyla başvurur ve site sahipliğini kanıtlar (Priority: P1)

Bir e-ticaret sitesinin agent'ı, gateway'in merchant onboarding agent'ına "sitem bu,
gateway'inize kayıt olmak istiyorum" başvurusunu iletir. Gateway, aday sitenin herkese
açık tanıtım dosyasından (merchant descriptor) unvan, vergi kimliği beyanı, iletişim
e-postası ve webhook adresini okur; ardından adaya bir doğrulama bileti verir. Aday bu
bileti kendi sitesinde yayınlayarak alan adının gerçekten kendisine ait olduğunu kanıtlar.
Kanıt doğrulanınca başvuru bir **kayıt talebi (RegisterRequest)** olarak saklanır —
bu aşamada merchant HENÜZ oluşmaz; merchant'a dair hiçbir işlem (kimlik, veri, yetki)
yapılamaz. Talep oluştuğunda gateway yöneticisine bildirim e-postası gider. Kanıt
doğrulanamazsa talep dahi oluşmaz.

**Why this priority**: Akışın giriş kapısı — başvuru alınamıyor ve sahiplik
kanıtlanamıyorsa onboarding'in geri kalanı var olamaz. Sahtecilik ön-filtresi de burada.

**Independent Test**: Descriptor yayınlayan simüle bir aday site ile başvuru yapılır;
doğrulama bileti siteye konur, doğrulama geçer, bekleyen kayıt talebi ve admin bildirim
maili tek başına gözlemlenir. Bilet yayınlanmadan yapılan denemenin talep üretmediği ve
talep aşamasında merchant kaydı bulunmadığı ayrıca doğrulanır.

**Acceptance Scenarios**:

1. **Given** descriptor'ı erişilebilir bir aday site, **When** agent başvuru iletir ve
   doğrulama biletini sitesinde yayınlar, **Then** bekleyen bir kayıt talebi oluşur ve
   admin'e başvuru özetini içeren bildirim maili gider; merchant kaydı OLUŞMAZ.
2. **Given** doğrulama biletini yayınlamamış bir aday, **When** doğrulama denenir,
   **Then** talep oluşmaz; admin kuyruğuna iş düşmez.
3. **Given** descriptor dosyası erişilemeyen/eksik alanlı bir site, **When** başvuru
   iletilir, **Then** aday anlaşılır bir hata ile bilgilendirilir; talep oluşmaz.
4. **Given** süresi geçmiş bir doğrulama bileti, **When** doğrulama denenir, **Then**
   doğrulama reddedilir; aday yeni başvuru ile yeni bilet alabilir.
5. **Given** aynı alan adı için bekleyen bir talep veya kayıtlı bir merchant, **When**
   aynı alan adından yeni başvuru gelirse, **Then** mükerrer talep oluşmaz; aday mevcut
   durum hakkında bilgilendirilir.

---

### User Story 2 - Admin talebi değerlendirir; onayla merchant doğar (Priority: P1)

Gateway yöneticisi, yönetim ekranındaki yeni "Merchant Talepleri" sayfasında bekleyen
kayıt taleplerini görür (başvuru bilgileri + sahiplik kanıtının durumu). Onay verdiğinde
merchant KAYDI O ANDA oluşturulur (kimlik anahtarı — MerchantKey — mevcut davranışla bu
anda üretilir) ve merchant'ın iletişim adresine tek kullanımlık aktivasyon bağlantısı
içeren e-posta gönderilir. Ret verdiğinde talep **Rejected** olarak kapanır; merchant
hiç oluşmaz, o başvurudan kimlik/yetki edinilemez. Komisyon tanımı onayın ÖN KOŞULU
DEĞİLDİR; onaydan sonra yapılır.

**Why this priority**: İnsan kontrolü bu senaryonun bilinçli tasarım kararı — otomatik
kayıt riskli bulundu. Onay kapısı olmadan merchant doğamaz, key teslimi ve sonrası
çalışamaz.

**Independent Test**: Bekleyen bir talep varken admin sayfası açılır; onay yolunda
merchant kaydının oluştuğu ve aktivasyon mailinin gittiği; ret yolunda talebin
kapandığı ve merchant'ın hiç oluşmadığı tek başına doğrulanır.

**Acceptance Scenarios**:

1. **Given** bekleyen bir kayıt talebi, **When** admin onaylar, **Then** merchant kaydı
   oluşur ve descriptor'daki iletişim adresine tek kullanımlık aktivasyon bağlantısı
   içeren e-posta gider.
2. **Given** bekleyen bir kayıt talebi, **When** admin reddeder, **Then** talep Rejected
   olarak kapanır; merchant oluşmaz, kimlik edinilemez.
3. **Given** reddedilmiş bir talebin alan adı, **When** aynı alan adından yeni başvuru
   yapılır, **Then** yeni başvuru normal akışla (yeni kanıt + yeni inceleme) işlenir.
4. **Given** bekleyen bir talep, **When** komisyon tablosu tanımlanmamışken admin
   onaylar, **Then** onay engellenmez (komisyon onay sonrası sürecin konusudur).

---

### User Story 3 - Merchant aktivasyon sayfasından MerchantKey'ini alır (Priority: P1)

Merchant, e-postadaki tek kullanımlık bağlantıya tıklayarak gateway'in aktivasyon
sayfasını açar ve MerchantKey'ini yalnız orada, bir kez görür. MerchantKey e-posta,
sohbet veya agent kanalında asla düz metin taşınmaz. Key teslim edildiğinde merchant
**Provisioning** statüsüne geçer: artık kimlik edinebilir (token alabilir) ama yetkisi
sınırlıdır — kendi kaydını okuma/tamamlama, settlement hesabı yönetimi, ReturnUrl bildirme
ve kart-saklama hazırlığı açık; ödeme çekimi (charge) kapalıdır. (Komisyonu merchant
belirlemez/yanıtlamaz — gateway-otoriter; yalnız Excel ile bilgilendirilir.) Bağlantı ikinci
kez kullanılamaz ve süresi dolunca geçersizleşir.

**Why this priority**: Kimlik bilgisinin güvenli (out-of-band) teslimi senaryonun
güvenlik omurgası; key olmadan merchant hiçbir makine işlemi yapamaz.

**Independent Test**: Onayla oluşmuş bir merchant'ın aktivasyon bağlantısı açılarak
key'in bir kez gösterildiği, merchant'ın Provisioning'e geçtiği, aynı bağlantının
ikinci denemede reddedildiği ve alınan token'ın charge yetkisi taşımadığı bağımsız
doğrulanır.

**Acceptance Scenarios**:

1. **Given** onayla oluşmuş bir merchant ve kullanılmamış aktivasyon bağlantısı,
   **When** bağlantı açılır, **Then** MerchantKey bir kez gösterilir ve merchant
   Provisioning statüsüne geçer.
2. **Given** kullanılmış bir aktivasyon bağlantısı, **When** tekrar açılır, **Then**
   istek reddedilir; key yeniden gösterilmez.
3. **Given** süresi dolmuş bir aktivasyon bağlantısı, **When** açılır, **Then** istek
   reddedilir; admin yeni aktivasyon maili tetikleyebilir.
4. **Given** Provisioning'e geçmiş bir merchant, **When** kimliğiyle token alır,
   **Then** token yalnız sınırlı yetki setini taşır; ödeme çekimi yetkisi içermez.
5. **Given** aktivasyonu henüz yapılmamış (key teslim edilmemiş) bir merchant, **When**
   token istenir, **Then** istek reddedilir (statü-kapılı verme).

---

### User Story 4 - Komisyon onay sonrası gateway-otoriter belirlenir; merchant Excel ile bilgilendirilir (Priority: P2)

Onaydan sonra admin, merchant'a özel komisyon tablosunu (grid) mevcut yönetim ekranından
tanımlar. Grid önce **Draft** aşamasındadır: admin hücreleri kademeli doldurur (kısmi olabilir).
Komisyon **gateway'in belirlediğidir**; merchant pazarlık edemez, kendi oranını öneremez,
kabul/ret etmez — gateway'in dediği oranların dışına çıkamaz. Grid, ilgili bankaların
desteklediği **tüm taksit sayılarını** kapsar. Admin grid'i **finalize** ettiğinde (bütünlük:
tüm servis-edilebilir kombinasyonlar fiyatlı, banka tabanı altında hücre yok) grid **Ready**
olur ve sistem "komisyon grid hazır" koşulunu (Active'e giden üç koşuldan biri) sağlar; bu
koşul ayrı context'te (Commission) gerçekleştiğinden Merchant'a **olay (event)** ile taşınır.

Merchant, Ready grid'i bilgi olarak **Excel eki içeren bir e-posta** ile alır (bağlayıcı
değil; kabul aksiyonu yoktur). Excel'in üretilip gönderilmesi gateway'in açtığı MCP yüzeyleri
üzerinden orkestre edilir (merchant bilgisi + grid okuma + Excel üretme + mail atma ayrı
generic MCP araçlarıdır); orkestrayı süren harici LLM/MCP client seçimi bu sürümün kapsamı
dışındadır (araçlar tek tek doğrulanır). Draft grid'den Excel üretilmez / event atılmaz.

**Why this priority**: "Komisyon grid hazır" Active'e geçişin üç koşulundan biri — ama
US1-US3 hattı tamamlanmadan anlamı yok. Pazarlık/kabul/ret akışı bu sürümde YOK; gerçek
müzakere (e-posta cevabı + niyet sınıflandırma) ayrı bir sürüme (014) bırakıldı.

**Independent Test**: Provisioning'de bir merchant için grid tanımsızken Excel üretilemediği
ve koşulun sağlanmadığı; admin grid'i tanımlayınca "grid hazır" koşulunun event ile Merchant'a
ulaştığı; MCP araçlarıyla grid'in tüm taksit satırlarıyla Excel'e döküldüğü ve mailin ekiyle
gönderilebildiği bağımsız doğrulanır.

**Acceptance Scenarios**:

1. **Given** komisyon grid'i Draft (veya hiç tanımlanmamış) Provisioning'de bir merchant,
   **When** grid okuma aracı çağrılır, **Then** "hazır değil (Draft)" döner; Excel üretilmez,
   koşul sağlanmaz.
2. **Given** Draft grid'i bütünlüklü doldurulmuş bir merchant, **When** admin grid'i finalize
   eder (Ready), **Then** "komisyon grid hazır" koşulu event ile Merchant'a ulaşır (Active
   koşulu #2).
3. **Given** Ready komisyon grid'i, **When** MCP araçları sırayla çağrılır (merchant bilgisi
   → grid oku → Excel üret → mail at), **Then** merchant'ın iletişim adresine grid'in tüm
   taksit satırlarını içeren Excel ekli mail gider.
4. **Given** kabul/ret akışı YOK, **When** merchant komisyonu değiştirmeye/pazarlığa
   çalışır, **Then** böyle bir uç yoktur; komisyon gateway-otoriter kalır.
5. **Given** Ready grid sonradan değiştirilir, **When** yeni değerler girilir, **Then** koşul
   zaten sağlanmıştır (tek-yön); merchant istenirse yeni Excel ile bilgilendirilebilir.

---

### User Story 5 - Koşullar tamamlanınca merchant otomatik Active olur (Priority: P2)

Merchant, sınırlı yetkisiyle eksiklerini tamamlar: payout banka hesabını (settlement
account) tanımlar ve ödeme dönüş adresini (ReturnUrl) bildirir; üçüncü koşul olan **komisyon
grid'inin hazır olması** admin tarafından sağlanır ve Merchant'a event ile ulaşır (merchant
aksiyonu değil — komisyon gateway-otoriter). Üç koşulun üçü de tamamlandığı anda sistem
merchant'ı kendiliğinden **Active**
statüsüne geçirir; bundan sonra alınan token'lar tam yetkiyi (ödeme çekimi dahil) taşır.
Koşullardan herhangi biri eksikken ödeme çekimi yetkisi hiçbir koşulda verilmez.

**Why this priority**: Onboarding'in bitiş çizgisi; ama önceki halkalar olmadan tek
başına değer üretmez.

**Independent Test**: Provisioning'de bir merchant'a üç koşul sırayla tamamlatılır;
üçüncüsü tamamlanır tamamlanmaz statünün kendiliğinden Active olduğu ve yeni token'ın
tam yetki taşıdığı; iki koşulla Active olunmadığı bağımsız doğrulanır.

**Acceptance Scenarios**:

1. **Given** üç koşuldan ikisi tamam bir merchant, **When** son koşul tamamlanır,
   **Then** merchant insan müdahalesi olmadan Active statüsüne geçer.
2. **Given** koşulları eksik bir merchant, **When** token alır, **Then** token ödeme
   çekimi yetkisi taşımaz.
3. **Given** Active'e geçmiş bir merchant, **When** yeni token alır, **Then** token tam
   yetki setini (ödeme çekimi dahil) taşır.
4. **Given** Active bir merchant, **When** admin onu Suspended/Passive yapar, **Then**
   mevcut statü-kapılı davranış aynen çalışır (token verilmez); onboarding bu davranışı
   değiştirmez.

---

### User Story 6 - Merchant işlemlerini kendi referansıyla eşleyebilir (Priority: P3)

Merchant, gateway'e gönderdiği isteklerde kendi tarafındaki sipariş/müşteri karşılığını
temsil eden opak bir referans (externalRef) iletebilir. Gateway bu değeri anlamlandırmaz
ve son kullanıcı kimliği tutmaz; yalnız saklar ve ilgili kayıtları dönerken aynen geri
verir. Merchant kendi yönetim panelinde kullanıcı/sipariş bazlı eşlemeyi bu referansla
kendisi yapar.

**Why this priority**: Küçük bir sözleşme alanı; asıl kullanımı ödeme çekimi (G5)
geldiğinde genişleyecek. Şimdi eklenmesi ileriye dönük kırılmayı önler.

**Independent Test**: externalRef ile yapılan bir isteğin kaydında değerin saklandığı ve
sorgu yanıtında aynen döndüğü bağımsız doğrulanır.

**Acceptance Scenarios**:

1. **Given** externalRef içeren bir istek, **When** kayıt oluşturulur ve sonra
   sorgulanır, **Then** externalRef değeri değiştirilmeden geri döner.
2. **Given** externalRef içermeyen bir istek, **When** işlenir, **Then** istek normal
   çalışır (alan zorunlu değildir).

---

### Edge Cases

- Descriptor dosyası var ama zorunlu alanları eksik/bozuk → başvuru anlaşılır hata ile
  reddedilir, talep oluşmaz.
- Doğrulama bileti süresi dolmuş → yeniden başvuru gerekir; eski bilet işe yaramaz.
- Aynı alan adından mükerrer başvuru (bekleyen talep veya kayıtlı merchant varken) →
  ikinci talep açılmaz.
- Bildirim maili gönderilemiyor (mail altyapısı erişilemez) → onboarding durumu tutarlı
  kalır; gönderim başarısızlığı kaybolmaz (yeniden denenebilir veya admin ekranından
  görülebilir), akış sessizce "başarılı" sayılmaz.
- Aktivasyon bağlantısı ikinci kez / süresi geçince kullanılırsa → ret; admin yeni
  aktivasyon maili tetikleyebilir.
- Talep aşamasında (merchant henüz yokken) kimlik/token/merchant işlemi denemesi → ret;
  merchant var olmadığı için hiçbir merchant ucu çalışmaz.
- Aktivasyon öncesi (key teslim edilmemişken) token isteği → ret (statü-kapılı verme).
- Provisioning'de charge yetkisi gerektiren istek → ret; koşul eksikken hiçbir yoldan
  charge yetkisi verilmez (fail-closed).
- Komisyon grid'i Draft'ken (finalize edilmemiş) → grid okuma "hazır değil" döner, Excel
  üretilmez, "grid hazır" koşulu sağlanmaz.
- "Komisyon grid hazır" koşulu tek-yöndür: bir kez sağlanınca (Ready) geri alınmaz;
  Active'e geçmiş merchant sonraki grid düzenlemesinden etkilenmez.
- Komisyon gateway-otoriterdir: merchant kabul/ret/pazarlık YAPAMAZ; böyle bir uç yoktur.
  (Müzakere — e-posta cevabı + niyet sınıflandırma — ayrı sürüme, 014'e bırakıldı.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, merchant adaylarının başvurusunu agent-agent (A2A) kanalından kabul
  etmek ZORUNDADIR; başvuru, aday sitenin alan adını içerir.
- **FR-002**: Sistem, aday sitenin herkese açık merchant descriptor dosyasından
  (`/.well-known/merchant-descriptor.json`) unvan, vergi kimliği beyanı, iletişim
  e-postası ve webhook adresini okumak ZORUNDADIR; dosya erişilemez veya zorunlu alanlar
  eksikse başvuru talep oluşturmadan reddedilir.
- **FR-003**: Sistem, alan adı sahipliğini tek kullanımlık ve süreli bir doğrulama
  bileti ile kanıtlatmak ZORUNDADIR: bilet adaya verilir, aday sitesinde
  (`/.well-known/merchant-challenge/{token}`) yayınlar, sistem eşanlı (senkron) doğrular.
  Doğrulama geçmeden kayıt talebi OLUŞMAZ.
- **FR-004**: Doğrulama geçen başvuru, merchant'tan AYRI bir **kayıt talebi
  (RegisterRequest)** olarak Pending durumunda saklanır. Talep aşamasında merchant
  kaydı OLUŞMAZ; merchant'a dair hiçbir işlem (kimlik edinme, veri, yetki) yapılamaz.
- **FR-005**: Pending talep oluştuğunda yapılandırılmış admin adresine başvuru özetini
  içeren bildirim e-postası gönderilir. E-posta gönderimi, gateway'in MCP istemcisi
  olarak bağlandığı SMTP-backed mail MCP server üzerinden yapılır.
- **FR-006**: Yönetim ekranında bekleyen talepleri listeleyen, başvuru ayrıntısını ve
  sahiplik kanıtı durumunu gösteren bir "Merchant Talepleri" sayfası bulunur; admin
  buradan onaylar veya reddeder. (İleride bu sayfa rol korumasına alınacaktır — RBAC
  ayrı feature.)
- **FR-007**: Onay üzerine merchant kaydı O ANDA oluşturulur (MerchantKey mevcut
  davranışla bu anda üretilir) ve merchant'ın descriptor'daki iletişim adresine tek
  kullanımlık, süreli aktivasyon bağlantısı içeren e-posta gönderilir. Komisyon tanımı
  onayın ön koşulu DEĞİLDİR.
- **FR-008**: Ret üzerine talep Rejected olarak kapanır; merchant oluşmaz. Reddedilen
  alan adından yeni başvuru normal akışla yeniden yapılabilir.
- **FR-009**: Aktivasyon sayfası Identity.Server üzerinde barındırılır; bağlantıdaki
  tek kullanımlık bilet doğrulanınca MerchantKey YALNIZ bu sayfada ve bir kez gösterilir.
  MerchantKey e-posta, sohbet, A2A veya agent kanallarında düz metin TAŞINMAZ.
- **FR-010**: Key teslimi merchant'ı **Provisioning** statüsüne geçirir. Provisioning
  token'ı sınırlı yetki demeti taşır: kendi kaydını okuma/tamamlama, settlement hesabı
  yönetimi, ReturnUrl bildirme ve kart-saklama hazırlığı; ödeme çekimi yetkisi İÇERMEZ.
  Aktivasyon öncesi token verilMEZ. (012'nin "yalnız Active token alır" kuralı
  "Provisioning sınırlı, Active tam" olarak genişler — anayasa amendment'ı gerektirir.)
- **FR-011**: Komisyon grid'i onaydan SONRA admin tarafından mevcut yönetim ekranından
  tanımlanır. Grid önce **Draft**'tır (kademeli doldurulur, kısmi olabilir) ve ilgili
  bankaların desteklediği **tüm taksit sayılarını** kapsar. Komisyon **gateway-otoriterdir**;
  merchant kendi oranını belirleyemez / pazarlık edemez.
- **FR-012**: Admin grid'i **finalize** ettiğinde grid **Ready** olur; finalize bütünlük
  gerektirir (tüm servis-edilebilir kombinasyonlar fiyatlı, banka tabanı altında hücre yok).
  Ready anında `MerchantCommissionGridReady` olayı yayınlanır (Active koşulu #2). Draft
  grid'den olay atılmaz, Excel üretilmez (fail-closed).
- **FR-013**: Merchant komisyon **kabul/ret/pazarlık YAPMAZ**; komisyon gateway'in dediğidir.
  Merchant grid'i yalnız **bilgi olarak** (Excel eki içeren e-posta) alır; bu e-posta bağlayıcı
  değildir ve bir kabul aksiyonu içermez. (Kabul/ret + e-posta cevabı + niyet sınıflandırma
  ayrı sürüme — 014 — bırakılmıştır.)
- **FR-014**: Komisyon Excel'inin üretilip mail'lenmesi gateway'in açtığı **MCP yüzeyleri**
  üzerinden orkestre edilir (merchant bilgisi okuma + grid okuma + Excel üretme + mail atma =
  ayrı generic MCP araçları). Orkestrayı süren harici LLM/MCP client seçimi bu sürümün
  kapsamı DIŞINDADIR; MCP yüzeyleri tek tek doğrulanır. Bu yüzeyler dışa/merchant'a kapalıdır
  (admin-düzlemi token, scope-korumalı).
- **FR-015**: Merchant, sınırlı yetkisiyle ödeme dönüş adresini (ReturnUrl)
  tanımlayabilir/güncelleyebilir; değer geçerli bir HTTPS adresi olmalıdır.
- **FR-016**: Şu üç koşulun üçü de sağlandığında sistem merchant'ı insan müdahalesi
  olmadan **Active** statüsüne geçirir: (1) en az bir settlement hesabı tanımlı,
  (2) komisyon grid'i **Ready** (finalize edilmiş), (3) ReturnUrl tanımlı. Koşul #2 ayrı
  context'te (Commission) gerçekleştiğinden bilgisi olay (event) ile taşınır. Koşul #2
  merchant aksiyonu DEĞİL — gateway-otoriter (admin finalize).
- **FR-017**: Active statüsü mevcut statü-kapılı kimlik mekanizmasıyla tam yetki
  demetini açar (ödeme çekimi dahil). Mevcut Passive/Suspended davranışı ve
  admin/merchant düzlem ayrımı (AdminPlaneOnly/MerchantScoped) DEĞİŞMEZ.
- **FR-018**: Merchant'a dönük kayıt uçları opsiyonel opak `externalRef` alanını kabul
  eder, saklar ve ilgili kayıtları dönerken aynen geri verir. Gateway son kullanıcı
  kimliği TUTMAZ; alanın anlamı merchant tarafındadır.
- **FR-019**: Mail gönderim başarısızlıkları akışı bozmaz ve sessizce kaybolmaz:
  onboarding durumu tutarlı kalır, başarısız gönderim yeniden denenebilir veya admin
  tarafından görülebilir.
- **FR-020**: Aynı alan adı için bekleyen bir talep veya kayıtlı bir merchant varken
  yeni başvuru mükerrer talep oluşturmaz.

### Key Entities

- **RegisterRequest (YENİ)**: Merchant'tan ayrı kayıt talebi — alan adı, doğrulanmış
  descriptor kopyası (unvan, vergi kimliği beyanı, iletişim e-postası, webhook adresi),
  sahiplik kanıtı sonucu, durum (Pending/Approved/Rejected), değerlendirme bilgisi.
  Merchant ancak onayla bu talepten doğar.
- **Merchant (mevcut aggregate, genişler)**: Statü seti Provisioning ile genişler
  (mevcut Active/Passive/Suspended korunur); ReturnUrl alanı ve Active koşullarının
  takibi eklenir. PendingReview/Rejected merchant statüsü DEĞİLDİR — o kavramlar
  RegisterRequest'te yaşar.
- **Doğrulama Bileti (domain challenge)**: Tek kullanımlık, süreli sahiplik kanıtı
  bileti; hangi alan adı için üretildiğini ve sonucunu tutar.
- **Aktivasyon Bileti**: Tek kullanımlık, süreli key-teslim bileti; kullanım anı ve
  durumu tutulur.
- **Komisyon Grid'i (mevcut, durum eklenir)**: Merchant'a özel oran grid'i; durum
  **Draft/Ready** (finalize ile Ready). Ready = Active koşulu #2 kaynağı (event ile taşınır).
  Kabul/sürüm/ret kaydı YOK (gateway-otoriter; müzakere 014).
- **Onboarding Bildirimleri (mail kayıtları)**: Yalnız **deterministik** mailler — admin
  "yeni başvuru" bildirimi ve aktivasyon maili; gönderim durumu izlenebilir (FR-019). Komisyon
  Excel maili agentik/harici LLM olduğundan domain kaydı tutmaz.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Bir merchant adayı, başvurudan MerchantKey teslimine kadar admin onayı
  dışında hiçbir manuel gateway müdahalesi olmadan ilerleyebilir.
- **SC-002**: MerchantKey hiçbir e-posta, sohbet veya agent mesajında düz metin yer
  almaz; yalnız tek kullanımlık sayfada bir kez görüntülenir.
- **SC-003**: Sahiplik kanıtı başarısız başvuruların %100'ü talep oluşturmadan
  reddedilir; admin kuyruğuna sahte/doğrulanmamış talep düşmez.
- **SC-004**: Onay öncesi (talep aşamasında) merchant kimliği/verisi üzerinde işlem
  denemelerinin %100'ü sonuçsuz kalır — merchant kaydı var olmadığından.
- **SC-005**: Kullanılmış veya süresi dolmuş aktivasyon bağlantısı denemelerinin %100'ü
  reddedilir.
- **SC-006**: Active koşulları eksik bir merchant'ın ödeme çekimi yetkisi edinme
  denemelerinin %100'ü reddedilir.
- **SC-007**: Üç koşulun sonuncusu tamamlandıktan sonra merchant 1 dakika içinde
  kendiliğinden Active olur ve yeni token'ı tam yetki taşır.
- **SC-008**: Admin komisyon grid'ini finalize eder etmez "grid hazır" koşulu (Active #2)
  event ile Merchant'a ulaşır; komisyon grid'i MCP araçlarıyla tüm taksit satırlarıyla Excel'e
  dökülüp merchant'a mail'lenebilir. Draft grid'den event/Excel üretilmez.

## Assumptions

- Ortam geliştirme/öğrenme ortamıdır: aday site canlı doğrulamada simüle edilir (lokal
  olarak descriptor + challenge dosyası sunan basit bir host yeterlidir).
- Mail altyapısı: **kendi generic `Mail.Mcp`** (SMTP-backed; dev'de Mailpit catch-all, gerçekte
  Gmail SMTP) + belge üretimi için generic **`Excel.Mcp`** (ClosedXML). İkisi de altyapı, BC
  değil, domain bilmez. Deterministik mailler `Common.IMailSender` ile BC'den; komisyon Excel
  maili harici LLM/MCP orkestrasyon. Admin bildirim adresi yapılandırmadan gelir.
- Komisyon = **gateway-otoriter (B)**: merchant kabul/ret/pazarlık yapmaz; grid Draft→Ready
  (admin finalize); kabul/sürüm/müzakere YOK (014'e bırakıldı).
- Doğrulama bileti ve aktivasyon bağlantısı süreleri yapılandırılabilir; makul
  varsayılanlar sırasıyla ~1 saat ve ~24 saattir.
- MerchantKey üretimi mevcut davranışıyla korunur (merchant oluşturma anında üretilir —
  bu tasarımda oluşturma = onay anı); teslim edilene kadar hiçbir kanala çıkmaz, statü
  kapıları nedeniyle atıl durur.
- "Kart-saklama hazırlığı" yetkisi bu feature'da tanımlanır ancak gerçek kart saklama
  uçları ayrı feature'da (G5) gelir; bu feature yalnız yetki kademesini kurar.
- Onboarding agent'ı (Merchant.Agent) mevcut Payment.Agent deseninin kopyasıdır; LLM
  yalnız araç sırası kurar, kimlik/sır/karar üretmez.
- ECommerce repo tarafındaki işler (descriptor yayını, challenge ucu, A2A istemcisi —
  E1) bu spec'in kapsamı dışında ayrı bağımlılıktır; canlı doğrulamada simüle edilir.
- Kapsam dışı: Admin RBAC (ayrı spec; yönetim ekranı şimdilik kimliksiz dev statükosunda),
  gerçek kart vault + ödeme çekimi (G5), DB-per-tenant (G4), MerchantKey rotasyonu.
- Anayasa etkisi: İlke V'teki "verme statü-kapılıdır (yalnız Active)" kuralı bu feature'la
  "Provisioning sınırlı demet, Active tam demet" olarak genişletildi — **amendment YAPILDI
  (anayasa v1.4.0, 2026-08-08)**.
