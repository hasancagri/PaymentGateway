# Feature Specification: Hosted Onboarding Form + Tek Kullanımlık Credential Teslimi

**Feature Branch**: `045-hosted-onboarding-form`

**Created**: 2026-09-19

**Status**: Draft

**Input**: ECommerce 078 kontratının PG tarafı implementasyonu — kontrat:
[contracts/pg-onboarding-rest.md](contracts/pg-onboarding-rest.md) (store repo'sundan kopya,
`specs/078-hosted-onboarding-form/contracts/` kaynak). Store admin agent'ı artık PII toplamaz:
PG hosted form linki açtırır; approve anında başvuru e-postasına tek kullanımlık teslim linki
mail'lenir; MerchantId+MerchantKey PG sayfasında BİR KEZ gösterilir.

**Kademe**: Tam — yeni dış S2S REST kontratı + iki yeni aggregate (form oturumu, teslim linki) +
iki hosted sayfa + approve'a mail tetiği + mevcut MCP teslim yolunun sökümü.

## Sorun (bugünkü durum)

029 akışı PII'yi ECom sohbetinden taşır: `submit_registration` MCP tool'u TCKN/IBAN dahil tüm
alanları alır; `registration_status` Approved'da MerchantId + MerchantKey döndürür (dev-açık karar).
PII ve uzun ömürlü sır LLM transkriptine sızar. 078/045 bu borcu iki uçtan kapatır: form PG'de
host edilir, teslim mail + tek kullanımlık PG sayfasıyla insan-aracılı olur.

## Clarifications

Kontrat kararları 078 store spec'inde verildi (clarify oturumu orada); PG tarafına bağlayanlar:

- Form linki süreli (~24 saat) + tek başvuruluk; süresi dolan link için store yeniden ister.
- Teslim linki süreli (~1 saat) + tek gösterimlik; PG Admin yeniden üretebilir, yenisi eskiyi öldürür.
- `registration_status`/durum yanıtı MerchantKey/MerchantId ASLA içermez; 404 yerine `status: "None"`.
- Doğrulama ucu yalnız `{valid: bool}` döner (ikili eşleşiyor + merchant Active); başka bilgi sızmaz.
- Auth: mevcut `ecommerce-onboarding` m2m istemcisi (merchant.read + merchant.write) aynen.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Store-tetikli form oturumu + hosted başvuru formu (Priority: P1)

Store, S2S REST ile e-posta vererek form oturumu açtırır; PG süreli + tek başvuruluk hosted form
linki döner. Müstakbel merchant formu PG ekranında doldurur (029 alan seti + doğrulamaları);
başvuru Pending doğar, PG Admin'e bugünkü bildirim maili gider. Aynı e-postada yaşayan Pending
başvuru varsa yeni oturum açılmaz (`formUrl: null` + `applicationStatus: "Pending"`).

**Why this priority**: Kontratın giriş kapısı; store US1'i (PII'siz başlatma) buna bağlı.

**Independent Test**: `POST /api/v1/onboarding/sessions` ile link alınır, form doldurulur;
RegisterRequest'in Pending doğduğu ve store'a hiç PII dönmediği görülür.

**Acceptance Scenarios**:

1. **Given** geçerli e-posta + makine token'ı, **When** store oturum açtırır, **Then** yanıt
   `{formUrl, expiresAt, applicationStatus}` döner ve link tarayıcıda formu açar.
2. **Given** form dolduruldu, **When** gönderilir, **Then** başvuru Pending doğar (mevcut
   tip-uyum/IBAN/e-posta doğrulamaları formda çalışır) ve teşekkür sayfası görünür.
3. **Given** aynı e-postada Pending başvuru, **When** yeni oturum istenir, **Then** `formUrl: null`
   + `applicationStatus: "Pending"` döner; yeni kayıt açılmaz.
4. **Given** süresi dolmuş ya da başvurusu tamamlanmış form linki, **When** açılır/gönderilir,
   **Then** nötr "geçersiz link" sayfası döner; başvuru oluşmaz.

---

### User Story 2 - Approve → mail + tek kullanımlık teslim sayfası (Priority: P1)

PG Admin başvuruyu approve ettiği an (mevcut MCP tool'u), başvuru e-postasına içinde süreli +
tek kullanımlık teslim linki olan mail gider. Link PG'nin hosted sayfasını açar; MerchantId +
MerchantKey BİR KEZ gösterilir ve sayfa "store ekranına gir" yönergesi içerir. İkinci açılış /
süre sonu bilgi göstermez. PG Admin yeni teslim linki üretebilir (yeni MCP tool'u); yenisi
eskiyi öldürür, yeni mail gider.

**Why this priority**: Key'in sohbete girmeden teslim edilmesinin tek yolu; store US3'ün girdisi.

**Independent Test**: Approve sonrası Mailpit'te mail; ilk açılışta ikili görünür, ikincide
görünmez; regenerate eski linki öldürür.

**Acceptance Scenarios**:

1. **Given** Pending başvuru, **When** admin approve eder, **Then** merchant Active doğar
   (bugünkü davranış) VE başvuru e-postasına teslim linkli mail gider.
2. **Given** teslim linki ilk kez açılır, **When** sayfa yüklenir, **Then** MerchantId +
   MerchantKey bir kez gösterilir ve link tüketilir.
3. **Given** tüketilmiş/süresi geçmiş link, **When** tekrar açılır, **Then** nötr "yeniden teslim
   için PG Admin'e başvurun" sayfası döner; bilgi sızmaz.
4. **Given** Approved merchant, **When** admin yeniden-teslim tool'unu çağırır, **Then** eski
   link(ler) ölür, yeni mail gider; tool yanıtında key YER ALMAZ.

---

### User Story 3 - S2S durum sorgusu + credential doğrulama (Priority: P1)

Store, e-posta ile başvuru durumunu sorgular (`None|Pending|Approved|Rejected` + mesaj + ret
nedeni; kimlik/sır ASLA dönmez). Store credential-giriş ekranı kayıt anında ikiliyi PG'ye
doğrulatır: PG yalnız "ikili eşleşiyor ve merchant Active" bilgisini `{valid}` olarak söyler.

**Why this priority**: Store'un durum takibi + FR-013 anlık doğrulaması buna bağlı; US1-US2 ile
birlikte kontrat tamamlanır.

**Independent Test**: Durum ucu dört durumu doğru döner (bilinmeyen e-posta = `None`); validate
ucu doğru ikilide `true`, yanlış key/pasif merchant'ta `false` döner.

**Acceptance Scenarios**:

1. **Given** hiç başvurusu olmayan e-posta, **When** durum sorgulanır, **Then** 200 +
   `status: "None"` döner (404 değil).
2. **Given** Approved başvuru, **When** durum sorgulanır, **Then** yanıt yalnız durum + mesaj
   içerir; MerchantId/MerchantKey alanları YOKTUR.
3. **Given** geçerli MerchantId+Key ikilisi (Active merchant), **When** validate çağrılır,
   **Then** `{valid: true}`; yanlış key ya da Active-olmayan merchant'ta `{valid: false}`.

---

### User Story 4 - Eski MCP teslim yolunun sökümü (Priority: P2)

`submit_registration` + `registration_status` MCP tool'ları (029'un ECom-yönlü yüzeyi) sökülür:
başvuru artık yalnız hosted formdan, durum yalnız S2S REST'ten, teslim yalnız mail+sayfadan.
Söküm ancak store'un yeni akışı canlı doğrulandıktan sonra (078 FR-009 sıralaması ile aynı).

**Independent Test**: `/mcp` tool listesinde iki tool da yok; store yeni uçlarla uçtan uca çalışıyor.

**Acceptance Scenarios**:

1. **Given** yeni akış canlıda doğrulanmış, **When** söküm yapılır, **Then** `/mcp` yüzeyinde
   PII alan ya da key döndüren tool kalmaz; S2S REST uçları çalışmaya devam eder.

---

### Edge Cases

- Form oturumu yaşarken başvuru başka kanaldan doğarsa (yarış): form POST'u e-posta Pending
  kontrolünü kayıt anında tekrarlar; ikinci başvuru reddedilir (mevcut tekilleştirme kuralı).
- Form POST doğrulama hatası (eksik TCKN vb.): form sayfası alan hatalarıyla yeniden gösterilir;
  oturum tüketilmez (düzeltip yeniden gönderilebilir).
- Approved/Rejected e-postayla yeni oturum: yeni başvuru serbest (bugünkü kural — yalnız Pending
  bloklar); durum yanıtı en güncel başvuruyu esas alır.
- Reveal linki üretilmeden merchant Passive/Suspended olursa: sayfa yine gösterir (teslim,
  statüden bağımsız — key zaten üretildi); validate ise `false` döner (Active şartı).
- Mail gönderimi geçici düşerse: outbox + Mail.Worker retry (mevcut altyapı); approve işlemi
  mail yüzünden geri alınmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `POST /api/v1/onboarding/sessions` (merchant.write): e-posta alır; yaşayan Pending
  yoksa süreli (~24 saat) + tek başvuruluk form oturumu üretir, `{formUrl, expiresAt,
  applicationStatus}` döner; Pending varsa `formUrl: null` + `applicationStatus: "Pending"`.
- **FR-002**: Hosted form PG'de yaşar: `GET` form sayfası (029 alan seti; type'a koşullu
  TCKN/vergi alanları) + `POST` başvuru kaydı; doğrulama mevcut `RegisterRequest.Submit`
  kurallarıyla; başarıda oturum tüketilir, PG Admin bildirim maili bugünkü gibi gider.
- **FR-003**: Geçersiz/süresi dolmuş/tüketilmiş form token'ı her iki uçta nötr sayfa döner;
  token doğruluğu sızdırılmaz.
- **FR-004**: `GET /api/v1/onboarding/applications/{email}` (merchant.read): en güncel başvurunun
  `{status, message, rejectReason}` yanıtı; hiç başvuru yoksa `status: "None"`; MerchantId/
  MerchantKey bu yanıtta ASLA yer almaz.
- **FR-005**: Approve anında başvuru e-postasına tek kullanımlık, süreli (~1 saat) teslim linki
  içeren mail gider (outbox — DB commit'siz mail yok); approve'un merchant-doğurma davranışı
  değişmez.
- **FR-006**: Teslim sayfası MerchantId + MerchantKey'i BİR KEZ gösterir; gösterim linki tüketir;
  tüketilmiş/süresi geçmiş linkte nötr yönlendirme sayfası döner.
- **FR-007**: PG Admin yeni teslim linki üretebilir (merchant.admin MCP tool'u); yeni link
  üretimi aynı merchant'ın yaşayan linklerini öldürür ve yeni mail tetikler; tool yanıtında
  key/link içeriği YER ALMAZ (yalnız "mail gönderildi").
- **FR-008**: `POST /api/v1/onboarding/credentials/validate` (merchant.read): `{merchantId,
  merchantKey}` ikilisinin Active bir merchant'a ait olup olmadığını `{valid: bool}` ile söyler;
  başka hiçbir bilgi dönmez; ikili loglanmaz.
- **FR-009**: Yeni akış store ile canlı doğrulandıktan sonra `submit_registration` +
  `registration_status` MCP tool'ları sökülür (yüzeyde PII alan / key döndüren tool kalmaz).
- **FR-010**: Reveal/form token'ları sunucu-durumlu (tek kullanım geri çekilebilir olmalı),
  rastgele 256-bit URL-safe; loglara token yazılmaz.

### Key Entities

- **OnboardingFormSession (YENİ)**: store-tetikli form erişimi; e-posta, token, süre (~24 saat),
  tüketim işareti (başarılı başvuru anı). Pending-varken üretilmez.
- **CredentialRevealLink (YENİ)**: approve/regenerate ürünü teslim linki; merchant referansı,
  token, süre (~1 saat), tüketim işareti (ilk gösterim). Regenerate eskileri öldürür.
- **RegisterRequest (MEVCUT)**: başvuru aggregate'i; doğulma yolu MCP tool'undan hosted forma
  taşınır, doğrulama/statü makinesi değişmez.
- **Merchant (MEVCUT)**: değişmez; MerchantKey tek-emisyon noktası `registration_status`
  yanıtından teslim sayfasına taşınır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uçtan uca akışta (oturum → form → approve → teslim → validate) PII ve MerchantKey
  hiçbir S2S yanıtta ve MCP tool yanıtında geçmez; teslim yalnız mail+sayfa yoluyladır.
- **SC-002**: Teslim linki ikinci açılışta ya da süre sonunda bilgi göstermez (%100).
- **SC-003**: Store'un 078 quickstart S1-S3 senaryoları bu implementasyona karşı canlı PASS olur.
- **SC-004**: Söküm sonrası `/mcp` yüzeyinde PII isteyen ya da key döndüren tool sayısı sıfır.

## Assumptions

- E-posta = başvuru kimliği (029 kuralı sürer); durum sorgusu en güncel başvuruyu döner.
- Form alan seti + doğrulamalar 029/`RegisterRequest.Submit` ile birebir; formda yeniden yazılmaz,
  aggregate kuralları kullanılır.
- Hosted sayfalar Merchant.Api'de gömülü HTML olarak yaşar (Payment.Api 041 dönüş sayfası emsali;
  Razor/SPA kurulmaz). Sayfaların dış URL tabanı config'ten gelir (store 078 emsali).
- `ecommerce-onboarding` istemcisi ve scope seti değişmez; yeni uçlar mevcut scope'larla korunur.
- Admin approve/reject MCP yüzeyi (044) değişmez; approve'a yalnız mail+link tetiği eklenir.
- Store tarafı (ECommerce 078) kendi repo'sunda hazır; canlı doğrulama iki sistem birlikte koşarak
  yapılır (store quickstart'ı esas).
