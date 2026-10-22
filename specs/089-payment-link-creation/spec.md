# Feature Specification: PaymentLink Üretimi (Pay-by-Link ilk dilim)

**Feature Branch**: `pivot/bank-adapter-skeleton` (dizin: `089-payment-link-creation`)

**Created**: 2026-10-20

**Status**: Draft

**Input**: Phase-1 ilk dikey dilim. Kaynak: `docs/eventstorming/payment-pay-by-link.md` (storm Perde-1, adım 1-8 + yaşam döngüsü). Store PG'den tek-kullanımlık TTL'li ödeme linki ister; müşteri hosted sayfada güven bağlamını görür; **banka/simülatör entegrasyonu KAPSAM DIŞI** (sağlayıcı stub).

## Bağlam

Constitution v2.0.0 ile PG = tek Payment BC, ödeme yönü **pay-by-link**: kart bilgisi sohbete/agent'a girmez; PG'nin ürettiği tek-kullanımlık TTL'li link üzerinden PG hosted sayfada toplanır. Bu dilim o akışın **girişini** kurar: linkin doğuşu (store talebi) + müşterinin hosted sayfada güven bağlamını görmesi. Gerçek çekim/3DS/banka adapter'ı SONRAKİ dilim — bu pivotta "banka" = ileride geliştirilecek **sahte banka simülatörü** (ayrı repo, henüz YOK); sıra kararı "önce PG iskeleti, simülatör sonra". 089 ne gerçek banka ne simülatör bekler; "Öde" sağlayıcıya gitmez, stub döner. 088'de korunan `HostedPaymentSession` tohumu bu dilimde `PaymentLink` aggregate'ine evrilir.

## Clarifications

### Session 2026-10-20

- Q: Terminal (Expired/Cancelled) sonrası aynı (store, orderRef) ile yeni link üretilebilir mi? → A: Evet — aktif-tekil; idempotent-return yalnız aktif (Pending/Opened) link varken, tekillik aktif-duruma filtreli (partial unique).
- Q: Süre dolumu Expired'a nasıl taşınır — lazy mi watchdog mu? → A: Lazy (yalnız erişim anında TTL kontrol); proaktif watchdog sonraki dilim.
- Q: Varsayılan TTL + müşteri sayfadayken süre dolumu? → A: 30 dk + kısa grace (Opened oturuma ~5 dk ek pencere).
- Q: Link teslim yolu? → A: Yalnız sohbet/S2S; SMS/e-posta teslimi sonraki dilim.

## User Scenarios & Testing *(mandatory)*

Aktörler: **Store** (PG'nin müşterisi e-ticaret sitesi, agent'ı aracılığıyla S2S çağırır) · **Müşteri** (kart sahibi, hosted sayfayı açar).

### User Story 1 — Store ödeme linki üretir (Priority: P1)

Store, bir sipariş için PG'den ödeme linki ister (tutar, para birimi, sipariş referansı, dönüş adresi vererek). PG tek-kullanımlık, TTL'li bir link + opak ödeme referansı döndürür; store bu linki müşteriye (sohbette) sunar.

**Why this priority**: Pay-by-link akışının giriş noktası; linksiz hiçbir sonraki adım olmaz. Tek başına store'a değer verir (link elde eder) ve bağımsız test edilir.

**Independent Test**: Yetkili bir CreatePaymentLink çağrısı geçerli girdiyle bir link + referans döndürür; link kaydı Pending durumda, TTL'li, (store, orderRef) tekil.

**Acceptance Scenarios**:

1. **Given** yetkili store + geçerli istek (tutar>0, TRY, orderRef, dönüş adresi), **When** CreatePaymentLink çağrılır, **Then** Pending bir PaymentLink doğar; link URL + opak referans döner.
2. **Given** aynı (store, orderRef) için zaten AKTİF (Pending/Opened) bir link var, **When** tekrar CreatePaymentLink çağrılır, **Then** yeni link üretilmez; var olan link aynen döner (idempotent).
3. **Given** para birimi TRY değil ya da tutar ≤ 0, **When** CreatePaymentLink çağrılır, **Then** istek reddedilir, link üretilmez.

---

### User Story 2 — Müşteri linki açar, güven bağlamını görür (Priority: P1)

Müşteri linke dokunur; PG'nin hosted ödeme sayfası açılır ve **kart formundan önce** güven bağlamını gösterir: mağaza adı, sipariş özeti, tutar, son geçerlilik. Müşteri kendi siparişini tanır.

**Why this priority**: Pay-by-link'in benimsenme eşiği güven; "bu benim siparişim" tanıması kart girişinden önce gelmeli. MVP'nin müşteriye-dönük yarısı.

**Independent Test**: Geçerli açılış token'lı link GET edilince güven bağlamı render edilir ve link Opened'a geçer; token geçersizse sayfa açılmaz.

**Acceptance Scenarios**:

1. **Given** Pending bir link + geçerli açılış token'ı, **When** müşteri linki açar, **Then** güven bağlamı gösterilir ve link Opened'a geçer.
2. **Given** bilinmeyen/sahte açılış token'ı, **When** açma denenir, **Then** sayfa açılmaz (fail-closed, bulunamadı).
3. **Given** Opened bir linkte müşteri "Öde"ye basar, **When** sağlayıcı adımına gelinir, **Then** PG "sağlayıcı kullanılamıyor" (PROVIDER_UNAVAILABLE) döner; link terminal'e GEÇMEZ (bu dilimde çekim yok).

---

### User Story 3 — Link ölümü: süre dolumu ve iptal (Priority: P2)

Süresi dolan link Expired, store'un iptal ettiği link Cancelled terminal durumuna geçer; her iki halde de link açılamaz/ödenemez.

**Why this priority**: Linkin yaşam döngüsünü kapatır (güvenlik + temizlik); tek-kullanım/TTL değişmezlerini görünür kılar. P2 çünkü üretim + açılıştan sonra gelir.

**Independent Test**: TTL geçmiş bir linki açma reddedilir ve link Expired'dır; store iptal edince link Cancelled olur ve açılamaz.

**Acceptance Scenarios**:

1. **Given** son geçerliliği geçmiş bir link, **When** müşteri açmayı dener, **Then** açılmaz; link Expired kabul edilir.
2. **Given** Pending/Opened bir link, **When** store iptal eder, **Then** link Cancelled olur; sonraki açma/ödeme reddedilir.
3. **Given** terminal (Expired/Cancelled) bir link, **When** herhangi bir işlem denenir, **Then** reddedilir (tek-yön).

### Edge Cases

- Bilinmeyen/sahte açılış token'ı → sayfa açılmaz (fail-closed).
- Çift CreatePaymentLink (ağ retry / çift start) → idempotent tek link.
- Açılmış ama ödenmemiş link (abandon) + TTL dolumu → Expired (lazy, erişim anında; Opened oturuma ~5 dk grace).
- "Öde" basıldığında (sağlayıcı stub) → PROVIDER_UNAVAILABLE; link Pending/Opened kalır, süre dolana dek.
- Terminal link'e yeniden işlem → reddedilir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, yetkili bir store'un bir sipariş için (tutar + para birimi + sipariş referansı + dönüş adresi + mağaza adı + sipariş özeti) ödeme linki talep etmesine izin VERMELİ; yanıt link URL'i + opak ödeme referansı içerir. Mağaza adı + sipariş özeti istekte store tarafından taşınır (güven bağlamı kaynağı, FR-006; ayrı mağaza-profil BC'si YOK — İLKE I).
- **FR-002**: Her link yapılandırılabilir bir son geçerlilik süresi (TTL, varsayılan 30 dk) taşıMALI; süre dolunca açılamaz ve ödenemez.
- **FR-003**: Aynı (store, sipariş referansı) için AKTİF (Pending/Opened) bir link varken tekrar talep YENİ link üretMEMELİ; var olan linki döndürMELİ (idempotent başlatma). Terminal (Expired/Cancelled) link sonrası aynı referansla YENİ link üretilebilMELİ; tekillik yalnız aktif durumlara uygulanır (aktif-duruma filtreli kısıt).
- **FR-004**: Sistem yalnız TRY kabul etMELİ; başka para birimi ya da tutar ≤ 0 reddedilMELİ.
- **FR-005**: Link tek-kullanımlık olMALI — "tüketim" yalnız başarılı ödemedir; henüz ödenmemiş (Pending/Opened) link süre/iptal sınırına dek tekrar açılabilir.
- **FR-006**: Müşteri linki açtığında, kart bilgisi istenMEDEN ÖNCE güven bağlamı (mağaza adı + sipariş özeti + tutar + son geçerlilik) gösterilMELİ.
- **FR-007**: Link açılması tahmin-edilemez gizli bir token ile korunMALI (kimlik yok, anonim yüzey); bilinmeyen/geçersiz token hiçbir linki açamaMALI (fail-closed, İLKE V).
- **FR-008**: Store bir linki iptal edebilMELİ; iptal sonrası link Cancelled terminal olur, açılamaz/ödenemez.
- **FR-009**: Süresi dolan link Expired terminal duruma geçMELİ; açılmış-ama-ödenmemiş link süre dolunca Expired kabul edilir. Expired geçişi erişim anında (lazy) hesaplanır — bu dilimde proaktif watchdog YOK. Açık (Opened) oturuma, TTL dolumunda kısa bir grace (~5 dk) penceresi tanınMALI.
- **FR-010**: Bu dilimde "Öde" eylemi sağlayıcıya (banka/simülatör) gitMEMELİ; sistem PROVIDER_UNAVAILABLE döndürMELİ ve linki terminal'e taşıMAMALI (çekim sonraki dilim).
- **FR-011**: Kart numarası (PAN) PG kalıcı deposuna YAZILMAMALI ve log/iz/yanıtlarda GÖRÜNMEMELİ (İLKE VI); bu dilimde zaten sağlayıcıya iletilmez.
- **FR-012**: Link talebi yalnız yetkili store çağrısıyla yapılabilMELİ; tenant store kimliği istekte taşınır (bu dilimde geçici yetki — nihai model sonraki iş).

### Key Entities *(include if feature involves data)*

- **PaymentLink**: Bir sipariş için ödeme oturumu/ömrü. Alanlar (impl'den bağımsız): store kimliği, sipariş referansı, tutar, para birimi (TRY), dönüş adresi, mağaza adı, sipariş özeti, son geçerlilik (TTL), gizli açılış token'ı, durum. Durumlar: **Pending → Opened → terminal {Paid, Expired, Cancelled}** (Paid bu dilimde stub yüzünden erişilmez). Değişmezler: tek-kullanım, TTL, (store, orderRef) aktif-tekil, terminal tek-yön.
- **PaymentAttempt** *(ileride)*: Bir PaymentLink altında sağlayıcıya giden deneme; bu dilimde modellenmez (banka/simülatör yok).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Store, geçerli bir istekle **1 saniyeden kısa** sürede kullanılabilir bir ödeme linki alır.
- **SC-002**: Aynı sipariş referansıyla aktif-link varken yapılan tekrar taleplerin **%100'ü aynı linki** döndürür (çift link üretilmez).
- **SC-003**: Süresi dolmuş / iptal edilmiş / bilinmeyen-token'lı açma girişimlerinin **%100'ü reddedilir**.
- **SC-004**: Müşteri linki açtığında, kart alanı görünMEDEN önce sipariş + mağaza + tutar + son geçerlilik bilgisini görür (ödeme-öncesi tanıma %100).
- **SC-005**: Kart numarası hiçbir log/kayıt/yanıtta görünmez (denetimde PAN sızıntısı **sıfır**).

## Assumptions

- **TTL**: yapılandırılabilir; varsayılan 30 dk (pay-by-link müşteri gecikmesine pay bırakır; 041'in 15 dk token'ından ayrı). **Teyit (clarify):** varsayılan 30 dk + Opened oturuma ~5 dk grace.
- **Terminal sonrası re-create**: AKTİF link yokken (Expired/Cancelled sonrası) aynı orderRef ile YENİ link üretilebilir; idempotent-return yalnız aktif link varken. **Teyit (clarify):** aktif-tekil; terminal sonrası re-create serbest.
- **Abandon→Expired tetiği**: süre dolumu erişimde (lazy) kabul edilir. **Teyit (clarify):** lazy; watchdog sonraki dilim.
- **Yetki (geçici)**: store→gateway çağrısı makine token'ı (payment.write kapsamı) + istekte tenant store kimliği; nihai model (platform IdP + anonim müşteri secret-token) sonraki iş (İLKE V).
- **Hosted sayfa + açılış token'ı** 041 `HostedPaymentSession` + per-session secret token deseninin aynası; bu dilimde `PaymentLink`'e evrilir.
- **Teyit (clarify)**: yalnız sohbet/S2S teslim; SMS/email sonraki dilim. (Çift-create yarışı: seed'de (MerchantId,OrderRef) unique index zaten var → aktif-tekil için partial/filtreli uniqueye evrilir, plan detayı.)

## Hedef-dışı (bu dilim)

Banka/simülatör adapter + wire + 3DS + NonSecure + gerçek otorizasyon/çekim; ödeme-modeli seçimi (3DS vs NonSecure — banka dilimine; o dilimde varsayılan 3DS, seçen kart sahibi); red taksonomisi; durum-sorgu + mutabakat (`Process/`); routing/circuit-breaker; store'a imzalı webhook sonuç bildirimi (çekim olmadan sonuç yok); klasik SMS/email link teslimi; iade/iptal-sonrası para hareketi; nihai yetki modeli (platform IdP). Bunlar sonraki dilim(ler).