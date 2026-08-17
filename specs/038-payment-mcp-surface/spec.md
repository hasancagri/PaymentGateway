# Feature Specification: Ödeme Süreci A2A + MCP Üzerinden (Payment MCP Surface, 007 Dirilişi)

**Feature Branch**: `038-payment-mcp-surface`

**Created**: 2026-08-16

**Status**: Draft

**Input**: User description: "Ödeme sürecinin tamamı MCP üzerinden ilerlesin (PaymentGateway
tarafı). Sepet PaymentGateway'de OLMAZ — ECommerce'de kalır. Müşteri chat'te 'sepetimdeki
ürünleri almak istiyorum' dediğinde ECommerce ChatAgent kendi MCP'leriyle (sepet vb.) işlemi
toplar, prompt'u yorumlar ve ödeme kısmını PaymentGateway'e yönlendirir. Yönlendirme kanalı:
A2A → PG Payment.Agent (007 dirilişi; kullanıcı seçimi). Payment.Agent kendi LLM router'ıyla
PG Payment.Api'nin (022'de sökülen, geri kurulacak) /mcp tool'larını sırayla çağırır. Kural
016 korunur: MCP'yi yalnız agent çağırır; BC→BC iletişim MCP olmaz. Features/Agents +
<X>ForAgent slice deseni ve MCP tool → yalnız Agent slice kuralı aynen uygulanır."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A2A üzerinden taksit sorgusu (Priority: P1)

Giriş yapmış site müşterisi chat'e "sepetimdeki ürünler için taksit seçeneklerini göster"
yazar. ChatAgent sepet toplamını kendi araçlarıyla alır, müşterinin kayıtlı kartını belirler
ve yorumlanmış isteği (tutar + kart referansı) A2A ile gateway'in ödeme agent'ına gönderir.
Ödeme agent'ı gateway'in kendi MCP araçlarıyla taksit seçeneklerini toplar ve yanıtı A2A ile
geri döner; müşteri taksit sayısı + toplam tutar listesini görür.

**Why this priority**: Zincirin tamamını (ChatAgent yorumu → A2A → Payment.Agent → MCP →
domain) para riski olmadan uçtan uca kanıtlayan okuma ayağı. Çekim (US2) bu zemin olmadan
kurulamaz. 024'ün A2A kontratı (payment-gateway-agent / installment_quote) hazır bekliyor.

**Independent Test**: ChatAgent'a taksit sorusu sorularak tek başına test edilir; dönen
seçenekler sandbox taksit gerçeğiyle (test kartları: İş Maximum çok taksit, Halkbank tek
çekim) karşılaştırılır.

**Acceptance Scenarios**:

1. **Given** kayıtlı kartı olan müşteri ve dolu sepet, **When** chat'te taksit istenir,
   **Then** yanıt gateway ödeme agent'ından A2A ile gelir ve taksit sayısı + toplam tutar
   listelenir; ChatAgent gateway MCP'sini doğrudan çağırmaz.
2. **Given** taksit desteklemeyen kart (sandbox Halkbank), **When** taksit sorgulanır,
   **Then** yalnız tek çekim seçeneği döner ve asistan bunu açıkça söyler.
3. **Given** gateway ödeme agent'ı erişilemez durumda, **When** taksit istenir, **Then**
   asistan "bu işlem şu an yapılamıyor" der; sohbetin geri kalanı çalışmaya devam eder.

---

### User Story 2 - A2A üzerinden kayıtlı kartla çekim (Priority: P2)

Müşteri seçenekleri görüp "3 taksitle öde" der. ChatAgent açık onay alır, ardından
yorumlanmış çekim isteğini (tutar + taksit + kart referansı) A2A ile gateway ödeme agent'ına
gönderir. Ödeme agent'ı çekimi gateway MCP araçlarıyla yürütür (çekim iyzico'da gerçekleşir);
müşteriye ödeme numarası + durum döner. Siteye ait ara HTTP köprüsü süreçte yer almaz.

**Why this priority**: Gerçek para hareketi — asıl hedef; US1'in kurduğu zincir üzerinde
devreye alınır.

**Independent Test**: Onay sonrası çekim sandbox test kartıyla (İş Maximum, 3 taksit) uçtan
uca koşulur; gateway'de ödeme kaydı ve iyzico sandbox işlemi doğrulanır.

**Acceptance Scenarios**:

1. **Given** müşteri onay vermiş, **When** çekim isteği A2A ile gönderilir, **Then** başarıda
   ödeme numarası + durum müşteriye iletilir ve gateway'de ödeme kaydı oluşur.
2. **Given** sağlayıcı (iyzico) hata döner, **When** çekim sonuçlanır, **Then** asistan
   "ödeme alınamadı" der; teknik ayrıntı müşteriye sızmaz; yarım kayıt kalmaz.
3. **Given** müşteri onay vermemiş, **When** niyet belirsiz, **Then** çekim isteği A2A'ya hiç
   gönderilmez (onay kuralı ChatAgent yönergesinde).

---

### User Story 3 - Seçilen kartla işlem (kart çözümü ECommerce'de) (Priority: P3)

Birden çok kayıtlı kartı olan müşteri "kartlarımı göster" der; ChatAgent listeyi
ECommerce'in KENDİ cüzdan araçlarından getirir (kart listesi/varsayılan kart ECommerce'de
yaşar — gateway'e kart sorulmaz). Müşteri "şu kartımla 3 taksit yap" deyince ChatAgent
seçilen kartın referansını (vault token) A2A isteğine koyar; gateway taksit sorgusu ve
çekimi GELEN token'la yürütür. Gateway tarafına kartla ilgili yeni hiçbir şey yazılmaz.

**Why this priority**: Bugünkü akışın bilinen sınırı (yalnız varsayılan kart). US1/US2 tek
kartla da çalışır; ECommerce'deki chat oturum hafızası işiyle (ayrı oturumda yürüyor)
birleşince tam değerini bulur.

**Independent Test**: İki kartlı test müşterisiyle liste ECommerce cüzdanından çekilir,
ikinci kart seçilip taksit + çekim koşulur; işlemin seçilen karta gittiği sandbox'tan
doğrulanır.

**Acceptance Scenarios**:

1. **Given** çok kartlı müşteri, **When** kartlar listelenir, **Then** liste ECommerce
   cüzdan araçlarından gelir ve yalnız güvenli görüntü alanları içerir; gateway'e kart
   listeleme isteği HİÇ gitmez.
2. **Given** listeden seçilmiş kart, **When** taksit/çekim o kartla istenir, **Then** A2A
   isteği seçilen kartın token'ını taşır ve işlem o kartla gerçekleşir (varsayılana düşmez).

---

### Edge Cases

- Merchant (site) Active değilse: çekim fail-closed reddedilir (anayasa İlke V — charge yalnız
  Active).
- İptal edilmiş (Revoked) kartla istek: anlaşılır ret, sağlayıcıya gidilmez.
- Payment.Agent'ın MCP keşfinde tool bulunamazsa (rename/söküm): agent isteği reddeder,
  ChatAgent'a anlaşılır hata döner; müşteriye "şu an yapılamıyor" yansır.
- Aynı çekim isteğinin yinelenmesi (çift mesaj/A2A retry): çift çekim koruması KAPSAM DIŞI
  (kullanıcı kararı — bugünkü HTTP akışında da yok; sandbox'ta risk düşük). İleride ayrı iş.
- A2A yanıtı gecikir/kopar ama çekim gateway'de tamamlanmışsa: müşteri sonucu görmese de
  ödeme kaydı vardır — asistan durumu sorgulayabilmeli mi, kapsam mı? (US1 zinciri üzerinden
  ödeme durumu sorgusu ileride ayrı iş kabul edilir.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Ödeme gateway'i, ödeme sürecine ait işlemleri (taksit sorgusu, kayıtlı kartla
  çekim) MCP araçları olarak yeniden sunmalı (022'de sökülen yüzeyin dirilişi). Gateway
  KART YÖNETİMİ SUNMAZ: kart listeleme/seçim/varsayılan-kart kavramı ECommerce cüzdanında
  kalır; gateway her isteği hazır kart referansı (vault token) ile alır.
- **FR-002**: Bu MCP yüzeyini YALNIZ gateway'in kendi ödeme agent'ı (Payment.Agent) çağırır;
  ECommerce ChatAgent gateway MCP'sine doğrudan bağlanmaz. Hiçbir servis→servis veya BC→BC
  iletişim MCP kullanmaz (016 kuralı).
- **FR-003**: ChatAgent ile gateway arasındaki iletişim A2A üzerinden yürür: ChatAgent
  müşteri niyetini yorumlar, sepeti kendi araçlarıyla toplar (sepet gateway'e taşınmaz) ve
  yorumlanmış ödeme isteğini (tutar, taksit, kart referansı) A2A mesajı olarak gönderir.
- **FR-004**: Payment.Agent gelen A2A isteğini karşılar ve yalnız MCP araç sırasını kurar
  (007 kuralı: LLM tutar/kart/taksit ÜRETMEZ — hepsi istekten ve domain'den gelir).
- **FR-005**: Her MCP tool'u yalnız kendi Agent slice'ını (`Features/Agents/`, `<X>ForAgent`)
  çağırmalı; Commands/Queries slice'larına gitmemeli.
- **FR-006**: Taksit sorgusu, verilen kayıtlı kart + tutar için seçenekleri (taksit sayısı +
  toplam tutar) dönmeli; seçenek yoksa bunu açıkça belirtmeli.
- **FR-007**: Çekim, verilen kayıtlı kart + tutar + taksit ile gerçekleşmeli; başarıda ödeme
  numarası + durum, başarısızlıkta kullanıcıya gösterilebilir sonuç dönmeli (teknik istisna
  sızdırmadan).
- **FR-008**: PAN, CVC veya sağlayıcı kart sırları (cardUserKey/cardToken) hiçbir MCP/A2A
  istek-yanıtında yer almamalı; kart yalnız opak vault token'la refere edilir. Kart görüntü
  alanları (maskeli liste) ECommerce cüzdan araçlarının işidir, gateway yüzeyinin değil.
- **FR-009**: Çekim yetkisi statü-kapılı: yalnız Active merchant bağlamında çekim; diğer
  statülerde fail-closed ret.
- **FR-010**: A2A bacağının kimliği bu işte ERTELENİR (kullanıcı kararı, 024 ile tutarlı):
  A2A isteği merchant kimliği taşımaz; yetki yalnız Payment.Agent→/mcp bacağındaki makine
  token'ıyla (payment scope) sağlanır. Merchant-bağlamlı A2A auth ayrı (ertelenmiş) auth
  işine aittir. Statü kapısı (FR-009) gateway içinde yine uygulanır.
- **FR-011**: ECommerce tarafındaki eski ödeme yolu SÖKÜLÜR (kullanıcı kararı — tek yol A2A):
  Customer.Api'deki get_card_installments / charge_default_card MCP araçları, bunların Agent
  slice'ları ve PG'ye giden HTTP çekim köprüsü kaldırılır; ChatAgent persona yönergesi yeni
  A2A akışına göre güncellenir. Bu söküm ECommerce reposunda bu spec'in parçasıdır.

### Key Entities

- **Kayıtlı Kart (StoredCard)**: Gateway'de merchant-scoped saklı kart (032 Model A; PAN
  yok, müşteri alanı yok). Bu işte DEĞİŞMEZ — yalnız vault token'la çözülür; müşteri→kart
  eşlemesi ECommerce cüzdanındadır.
- **Ödeme (Payment)**: Çekim sonucu oluşan kayıt; ödeme numarası, tutar, taksit, durum.
- **Taksit Seçeneği**: Kart + tutar için sağlayıcıdan dönen seçenek; taksit sayısı ve toplam
  ödenecek tutar.
- **Ödeme İsteği (A2A)**: ChatAgent'ın yorumlayıp gönderdiği yapılandırılmış istek — tutar,
  taksit, kart referansı, merchant bağlamı. Sepet içeriği taşımaz.
- **Merchant (site)**: Gateway müşterisi site (ör. ECommerce); çekim yetkisi statüsüne bağlı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Chat'ten taksit sorgusu ve kayıtlı kartla çekim, ECommerce tarafında ara HTTP
  köprüsü olmadan, uçtan uca A2A + gateway MCP zinciriyle sandbox'ta tamamlanır (canlı
  senaryo: sepet → taksit listesi → onay → çekim → ödeme numarası).
- **SC-002**: Sandbox'ta en az bir çok-taksitli çekim başarıyla gerçekleşir; gateway ödeme
  kaydı iyzico sandbox işlemiyle eşleşir.
- **SC-003**: Hiçbir MCP/A2A yanıtında PAN/CVC/sağlayıcı token'ı bulunmaz (yanıt sözleşmeleri
  + canlı yanıt örnekleriyle doğrulanır).
- **SC-004**: Active olmayan merchant bağlamında çekim denemesi %100 reddedilir.
- **SC-005**: Kural denetimi temiz: **Payment.Api** /mcp yüzeyinin tek tüketicisi
  Payment.Agent'tır; ChatAgent veya herhangi bir BC kodu Payment.Api MCP client'ı içermez.
  (Merchant.Api onboarding /mcp'sinin ChatAgent tüketimi — 032 — bu kuralın DIŞINDA, aynen
  yaşar.)

## Assumptions

- **Sepet gateway'e girmez**: sepet toplama/yorumlama tamamen ECommerce ChatAgent'ta; gateway
  yalnız yapılandırılmış ödeme isteği görür (tutar + taksit + kart referansı).
- **Kart çözümü tamamen ECommerce'de**: müşteri→kart eşlemesi (varsayılan kart, liste,
  seçim) ECommerce cüzdanında yaşar; ChatAgent kart referansını (vault token) ECommerce
  cüzdan MCP araçlarından alır ve A2A isteğine koyar. Gateway'de kartla ilgili YENİ kod
  yazılmaz — gateway müşteri kavramını bilmez, kartı saklamadaki mevcut haliyle (StoredCard,
  merchant-scoped) yalnız token üzerinden çözer.
- **024 A2A kontratı zemin**: ECommerce'de duran payment-gateway-agent / installment_quote
  kontrat sabitleri ve A2A named-client altyapısı yeniden kullanılır; skill seti bu işle
  genişler (çekim, kart listesi).
- **Payment.Agent→/mcp kimliği mevcut desen**: Payment.Agent /mcp'yi makine token'ıyla
  (client_credentials, payment.write) çağırır — 011 modeli aynen.
- **Sandbox-only kapsam kuralı geçerli**: her senaryo iyzico sandbox test kartlarıyla
  doğrulanabilir olmalı.
- **Kart seçimi (US3) hafıza işinden bağımsız**: ECommerce'deki chat oturum hafızası işi
  çok-adımlı akışı iyileştirir ama US3'ün ön koşulu değildir.
- **Merchant onboarding MCP yüzeyi (Merchant.Api) kapsam dışı**, aynen kalır. ChatAgent'ın
  onboarding için PG /mcp'ye doğrudan bağlanması (032) da bu işin dışında — o admin persona
  akışıdır, ödeme değil.
- **Kart ekleme (tokenize) agent yüzeyinde YOK — güvenlik kararı (2026-08-16)**: kart
  ekleme/silme chat, MCP veya A2A üzerinden YAPILMAZ; PAN asla agent/LLM bağlamına girmez.
  Kart ekleme yalnız mevcut ekran yolundan (ECommerce kart formu → gateway tokenize HTTP
  ucu) sürer. Bu işte tokenize/revoke uçlarına dokunulmaz.
- **Çift çekim koruması kapsam dışı** (Q3=A): idempotency ileride ayrı iş; onay kuralı
  (ChatAgent yönergesi) tek koruma katmanı.
- **A2A auth ertelenmiş** (Q2=B): A2A bacağı kimliksiz (024 gibi); tek yetki katmanı
  Payment.Agent→/mcp makine token'ı. Bilinçli geçici kabul; ertelenmiş auth işinde kapanır.
- **Eski yol sökümü dahil** (Q1=A): iş iki repoya dokunur — PG (MCP yüzeyi + Payment.Agent
  skill'leri) ve ECommerce (Customer.Api ödeme MCP araçları + HTTP köprüsü sökümü, ChatAgent
  A2A bağlantısı + persona güncellemesi).