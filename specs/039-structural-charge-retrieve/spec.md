# Feature Specification: Yapısal İdempotent Çekim + Retrieve Yüzeyi

**Feature Branch**: `039-structural-charge-retrieve`

**Created**: 2026-08-18

**Status**: Draft

**Input**: User description: "PaymentGateway'e Order.Api'nin tükettiği yapısal (server-to-server REST,
merchant API key ile) idempotent çekim + retrieve yüzeyi ekle. Charge correlationKey KABUL eder;
key persist+indeks; aynı key → var olan ödemeyi döner (çift çekim yok). Retrieve: key ile ve
paymentId ile okuma. Basket kalemleri istekte gelmez; PG sentezler. Tüketici: ECom 039."

**Artefakt kademesi**: **Tam** — mevcut charge kontratı değişir (correlationKey + basket kaldırma),
Payment aggregate'e yeni kalıcı alan + indeks, yeni okuma (retrieve) uçları; para-kritik idempotency.

## Tüketici bağlamı (bu repo dışında, referans)

ECom `039-chat-order-completion` bu yüzeyi tüketir. Kontrat:
`ECom/specs/039-chat-order-completion/contracts/paymentgateway-charge-verify.md`.
Order.Api sunucu-tarafı durable `PaymentAttempt` akışı yürütür; PG'ye yapısal REST'le çekim yapar,
yanıt kaybında aynı `correlationKey` ile retrieve edip reconcile eder. `correlationKey` = Order'ın
`userId + sepet + taksit`'ten ürettiği **opak hex HMAC**; PG bunu yorumlamaz, salt idempotency
anahtarı olarak kullanır. Sahiplik anahtarın kendisindedir (F1) → PG'ye ayrı buyer referansı gerekmez.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - İdempotent yapısal çekim — çift çekim yok (Priority: P1)

Order.Api, merchant API key'iyle PG'ye `correlationKey` taşıyan bir çekim isteği yapar. İlk istekte
PG sağlayıcıdan (iyzico) tahsilatı yapar, ödemeyi `correlationKey` ile kalıcı saklar ve sonucu döner.
Aynı `correlationKey` ile ikinci istek gelirse PG **yeni tahsilat yapmaz** — var olan ödemeyi döner.

**Why this priority**: Feature'ın çekirdeği ve para bütünlüğünün temeli. Yanıt kaybında Order aynı
key ile tekrar dener; PG dedupe etmezse çift tahsilat olur — kabul edilemez.

**Independent Test**: Aynı `correlationKey` ile iki charge isteği gönderilir; sağlayıcıya yalnız BİR
tahsilat gider ve iki yanıt da aynı `paymentId` + `status` + `price` döner.

**Acceptance Scenarios**:

1. **Given** hiç kullanılmamış `correlationKey`, **When** charge isteği gelir, **Then** sağlayıcıdan
   tahsilat yapılır, ödeme key ile persist edilir, yanıt `paymentId`+`status:success`+`price` döner.
2. **Given** bir `correlationKey` ile başarılı ödeme zaten var, **When** aynı key ile charge tekrar
   gelir, **Then** yeni tahsilat YAPILMAZ; var olan ödeme aynı `paymentId`+`price` ile döner.
3. **Given** bir `correlationKey` ile başarısız ödeme kaydı var, **When** aynı key ile charge tekrar
   gelir, **Then** yeni tahsilat YAPILMAZ; var olan `status:failed` kaydı döner (deneme değişmez).
4. **Given** iki eşzamanlı charge isteği aynı `correlationKey` ile, **When** ikisi de işlenir, **Then**
   sağlayıcıya en fazla BİR tahsilat gider; ikisi de aynı ödemeyi döner (yarış idempotent çözülür).

---

### User Story 2 - Retrieve (verify + reconcile okuması) (Priority: P1)

Order.Api, sipariş öncesi doğrulama ve yanıt-kaybı kurtarma için ödemeyi `correlationKey` **veya**
`paymentId` ile okuyabilmelidir. Bugün Payment write-only; okuma yüzeyi yok. Yanıt charge ile aynı
alanları taşır. Bilinmeyen anahtar için "bulunamadı" döner (Order bunu belirsiz sayar, tekrar dener).

**Why this priority**: Kayıp-yanıt kurtarması ve para-kritik verify bu okumaya bağlı. Charge yanıtı
düşerse Order elde `paymentId` olmadan yalnız `correlationKey` ile ödemeyi bulabilmelidir.

**Independent Test**: US1'de oluşan bir ödeme, `correlationKey` ile GET edilir → aynı `status`+`price`
+`paymentId` döner. Bilinmeyen bir key ile GET → bulunamadı (404).

**Acceptance Scenarios**:

1. **Given** `correlationKey` ile başarılı ödeme var, **When** o key ile retrieve çağrılır, **Then**
   `paymentId`+`status:success`+`price`+`paidPrice`+`currency`+`correlationKey` echo döner.
2. **Given** bir `paymentId`, **When** o id ile retrieve çağrılır, **Then** aynı ödeme alanları döner.
3. **Given** hiç kullanılmamış bir `correlationKey`, **When** retrieve çağrılır, **Then** "bulunamadı"
   döner (çekim yapılmaz; Order bunu belirsiz sayıp deadline'a dek yeniden dener).
4. **Given** başka merchant'ın ödemesi, **When** bir merchant kendi key/id'siyle olmayan kaydı ister,
   **Then** o merchant'ın kaydı görünmez (kiracı sınırı korunur).

---

### User Story 3 - Sepet kalemi sunucu-sentezi (Priority: P2)

Yeni yapısal çekim isteği **basket kalemi taşımaz** (Order kalem listesini PG'ye vermez — para
manipülasyonu yüzeyi kapanır). PG, sağlayıcının zorunlu tuttuğu sepet alanını `price` + buyer'dan
**sunucu tarafında sentezler** (tek satır). Mevcut `ChargePayment` basket zorunluluğu bu yolda kalkar.

**Why this priority**: Kontrat uyumu — Order kalem göndermez; ama iyzico sepet ister. Sentezleme
olmadan yapısal çekim çalışmaz. Para tutarı `price` ile taşındığından kalem içeriği sağlayıcı-teknik
zorunluluktur, iş anlamı taşımaz.

**Independent Test**: Basket alanı olmayan bir charge isteği gönderilir → çekim başarır (PG sepet
alanını kendi sentezler); sağlayıcı 5040 ("sepet zorunlu") hatası dönmez.

**Acceptance Scenarios**:

1. **Given** basket kalemi içermeyen charge isteği, **When** çekim yapılır, **Then** PG geçerli bir
   tek-satır sepet sentezler ve sağlayıcı çağrısı sepet-eksik hatası vermeden tamamlanır.
2. **Given** sentezlenen sepet, **When** tahsilat yapılır, **Then** sepet tutar toplamı çekim `price`
   ile tutarlıdır (sağlayıcı tutar-uyuşmazlık hatası dönmez).

---

### Edge Cases

- **Crash penceresi (çekim yapıldı, persist olmadı)**: Sağlayıcı tahsil etti ama PG kaydı yazmadan
  çöktü → aynı key ile retry çift çekebilir. Anahtar sağlayıcı çağrısından ÖNCE kalıcı işaretlenmeli
  (pending/attempt kaydı) ki retry var olanı bulsun; sağlayıcı-tarafı idempotency (conversationId) ek kat.
- **Aynı key, farklı tutar**: Bir key'e bağlı ödeme varken aynı key farklı `price` ile gelirse yeni
  tahsilat YAPILMAZ; var olan kayıt döner (key ödemeyi tekilleştirir; tutar anahtara bağlıdır).
- **Bilinmeyen key/id retrieve**: 404 bulunamadı — 500 değil (Order belirsiz sayıp reconcile eder).
- **Başarısız çekim + aynı key retry**: `failed` kayıt döner; yeniden çekim denenmez (deneme immutable).
- **Kiracı ihlali**: Bir merchant başka merchant'ın key/id'sini isterse kayıt görünmez (kiracı sınırı).
- **Geçersiz merchant API key / yetkisiz**: İstek reddedilir; hiçbir tahsilat/okuma yapılmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Charge ucu istekte bir `correlationKey` (opak string) KABUL etmeli ve bu anahtarı ödeme
  kaydında **kalıcı saklamalı + indekslemelidir** (bugün saklanmıyor).
- **FR-002**: Charge, `correlationKey` bazında **idempotent** olmalı: verilen key'e bağlı bir ödeme
  (başarılı VEYA başarısız) zaten varsa **yeni tahsilat YAPILMAZ**, var olan ödeme döner.
- **FR-003**: Eşzamanlı iki charge isteği aynı `correlationKey` ile gelse bile sağlayıcıya en fazla
  **bir** tahsilat gitmelidir (yarış koşulu idempotent çözülür; anahtar tekilliği kalıcı katmanda).
- **FR-004**: Charge yanıtı **zorunlu** şu alanları taşımalıdır: `paymentId` (sağlayıcı/PG kimliği),
  `status` (success/failed), `price` (temel tutar), `paidPrice` (tahsil edilen), `currency`,
  `correlationKey` (echo).
- **FR-005**: Sistem, bir ödemeyi `correlationKey` İLE okuyabilen bir **retrieve** ucu sağlamalıdır;
  yanıt charge yanıtıyla aynı alanları taşır.
- **FR-006**: Sistem, bir ödemeyi `paymentId` İLE okuyabilen bir retrieve ucu sağlamalıdır (aynı alanlar).
- **FR-007**: Bilinmeyen `correlationKey`/`paymentId` için retrieve **"bulunamadı"** (404) dönmeli;
  hata (5xx) veya boş-başarı DÖNMEMELİDİR (tüketici 404'ü "belirsiz" sayıp yeniden dener).
- **FR-008**: Yeni yapısal charge isteği **basket kalemi taşımamalı**; PG, sağlayıcının zorunlu
  tuttuğu sepet alanını `price` + buyer'dan **sunucu tarafında sentezlemelidir** (tek satır).
- **FR-009**: Charge ve retrieve uçları **merchant API key** (kullanıcı JWT'si değil) ile kimliklenmeli
  ve mevcut merchant-scoped yetki modeliyle korunmalıdır (yalnız Active merchant charge alır).
- **FR-010**: Retrieve **kiracı-sınırlı** olmalı: bir merchant yalnız kendi ödemelerini görebilir;
  başka merchant'ın key/id'siyle kayıt DÖNMEZ.
- **FR-011**: `correlationKey` PG için **opaktır**: içeriği yorumlanmaz/doğrulanmaz, salt tekillik +
  arama anahtarı olarak kullanılır (sahiplik/HMAC anlamı tüketici tarafındadır).
- **FR-012**: Sistem, çift-çekim riskini azaltmak için `correlationKey`'i **sağlayıcı çağrısından
  ÖNCE** kalıcı işaretlemeli; kayıp-yanıt sonrası retry, var olan girişimi bulup tekrar tahsil etmemeli.
- **FR-013**: Mevcut A2A charge yüzeyi bu değişiklikle çakışmamalı; taksit **sorgusu** (read-only)
  A2A/agent yolunda kalabilir. Yapısal charge para-hareketinin tek yoludur (039 tüketici için).

### Key Entities *(include if data involved)*

- **Ödeme (Payment)**: Mevcut aggregate. Bugün taşıdıkları: `MerchantId`, `VaultToken`, `Price`,
  `PaidPrice`, `Installment`, sağlayıcı komisyon/ücret, `ProviderPaymentId`, `Status` (Success/Failed).
  **Yeni**: `CorrelationKey` (opak string, indeksli, kiracı-içi tekil) — idempotency + retrieve anahtarı.
- **Ödeme Görünümü (Payment View)**: Retrieve/charge yanıtı — `paymentId`, `status`, `price`,
  `paidPrice`, `currency`, `correlationKey` echo. Kalıcı yeni tip gerektirmez (Payment'tan map).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Aynı `correlationKey` ile tekrarlanan çekim isteklerinin **%100'ünde** sağlayıcıya en
  fazla bir tahsilat gider (çift-çekim oranı %0).
- **SC-002**: `correlationKey` ile oluşan bir ödeme, aynı key ile retrieve edildiğinde **%100** aynı
  `paymentId`+`status`+`price` ile bulunur.
- **SC-003**: Basket kalemi içermeyen yapısal charge isteklerinin **%100'ü**, sağlayıcı sepet-eksik
  hatası (5040) almadan tamamlanır.
- **SC-004**: Bilinmeyen key/id retrieve istekleri **%100** "bulunamadı" (404) döner; hiçbiri 5xx dönmez.
- **SC-005**: Bir merchant, başka merchant'ın hiçbir ödemesini retrieve ile göremez (%0 kiracı sızıntısı).

## Assumptions

- **Sağlayıcı senkron, pending yok**: iyzico NonSecure çekim senkron `success`/`failed` döner; PG'de
  kalıcı bir `pending` durumu yoktur. Tüketici tarafındaki "belirsiz/pending", PG'nin **erişilemez
  olması veya retrieve'in bulamaması** durumudur — PG kendi kaydında pending taşımaz.
- **Anahtar opak**: `correlationKey` içeriği (HMAC, userId) PG için anlamsızdır; PG yorumlamaz.
  Sahiplik doğrulaması tüketicidedir (yalnız çağıran kendi anahtarını üretebilir → kendi ödemesini okur).
- **Buyer persist gerekmez**: Sahiplik anahtarda olduğundan (F1) PG'ye ayrı buyer referansı eklenmez;
  buyer yalnız sağlayıcı çağrısı için istekte gelir (persist edilmez — bugünkü davranış korunur).
- **Basket sentezi teknik**: Sentezlenen tek-satır sepet iş anlamı taşımaz; yalnız sağlayıcının wire
  zorunluluğunu karşılar. Tutar `price` ile taşınır; sepet toplamı `price` ile tutarlı sentezlenir.
- **Yol/versiyon**: Yeni uçlar mevcut `api/v{version}/merchants/{merchantId}/payments` grubuna eklenir;
  kesin yol/şekil plan aşamasında netleşir (bu spec NE/NEDEN'i sabitler).
- **Idempotency granülaritesi**: Bir `correlationKey` = en fazla bir ödeme kaydı (kiracı-içi tekil).
- **Migration**: Mevcut ödeme kayıtlarının `CorrelationKey`'i boş kalabilir (geriye dönük); yeni alan
  nullable/opsiyonel eklenir, yalnız yeni çekimlerde dolar.

## Dependencies

- **Tüketici**: ECom `039-chat-order-completion` (`PaymentGatewayClient` charge/retrieve). Bu feature
  o repo'nun canlı doğrulamasını (S1-S4) bloke eder; öncelik bu yüzden P1.
- **Mevcut PG**: `ChargePayment` (033/035) yeniden kullanılır/genişletilir; Payment aggregate (035
  VO'ları), merchant-scoped yetki (012/013), iyzico sağlayıcı entegrasyonu (034) aynen geçerli.
