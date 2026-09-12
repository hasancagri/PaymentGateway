# Feature Specification: Hosted Kart Saklama — Store 075 Sözleşme Hizalama

**Feature Branch**: `040-hosted-card-storage-075`

**Created**: 2026-09-12

**Status**: Draft

**Input**: Store (ECommerceWithAgentFramework) 075 kart-saklama modeline geçiş. Mağaza kendi yerel-vault
PAN yolunu söktü; artık PG'den **hosted kart-ekleme** (PAN yalnız sağlayıcının hosted formunda) + opak
`userHandle`/`cardHandle` ile listeleme/silme/NON-3D çekim bekliyor. Sözleşme birebir:
`ECommerceWithAgentFramework/specs/075-iyzico-card-storage/contracts/pg-card-contract.md`. Mevcut PG kart
modeli (032/033: iyzico cardUserKey/cardToken + saklı-kartla çekim) KORUNUR; değişen = **giriş yolu**
(PAN-POST `TokenizeCard` → hosted form) + **dış yüzey** (tek opak token → 075 şekli: session/list/handle).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Hosted Kart Ekleme (Priority: P1)

Son kullanıcı mağaza asistanından "kart ekle" der. Mağaza, PG'ye merchant kimliği + korelasyon kimliği
(conversationId) + dönüş adresi (callbackUrl) ile bir **oturum** açtırır. PG, sağlayıcının (iyzico)
**hosted kart-kaydetme formunu** başlatır ve tarayıcıda açılacak güvenli bir bağlantı (addUrl) döner.
Kullanıcı kart bilgisini yalnız sağlayıcının sayfasında girer — kart numarası ne mağazaya ne PG'ye
uğrar. Kullanıcı formu bitirince PG sonucu sağlayıcıdan çeker: sağlayıcı bir **kullanıcı-kimliği**
(cardUserKey; aynı kullanıcının sonraki kartları buna eklenir) verir. PG bunu oturuma yazar ve mağazaya
`pgUserHandle` olarak döner.

**Why this priority**: Modelin çekirdeği + uyum kazanımı. PAN hiçbir mağaza/PG sınırına girmez; 075'in
söktüğü PAN-POST yolunun yerini alır. Bu olmadan diğer yetenekler (liste/çekim) anlamsız.

**Independent Test**: Merchant token'ıyla oturum açılır → addUrl döner; sağlayıcının sandbox formunda test
kartı girilir; PG oturumu tamamlar → cardUserKey oturuma yazılır + mağazaya döner; kart numarası hiçbir
PG kaydında/loğunda yoktur. İptal/hata'da kalıcı kayıt oluşmaz.

### User Story 2 - Kayıtlı Kartları Listeleme (Priority: P1)

Mağaza, kullanıcının `userHandle`'ı (=cardUserKey) ile PG'den kayıtlı kartları ister. PG sağlayıcının
kart kümesini canlı çeker ve gösterilebilir alanları döner: marka, son 4 hane, son-kullanma, etiket ve
opak `cardHandle` (=cardToken; silme/çekim referansı). Kart numarası/CVV asla dönmez. Kullanıcının hiç
kartı yoksa boş liste (hata değil).

**Why this priority**: Ekleme MVP'sini tamamlar — eklenen kart görülmeden akış eksik. Silme ve çekim de
`cardHandle`'ı buradan alır.

**Independent Test**: cardUserKey kayıtlıyken liste çağrılır → kart(lar) marka+son4+cardHandle ile döner;
PAN/CVV yok. Handle yoksa boş liste.

### User Story 3 - NON-3D Saklı Kartla Çekim (Priority: P2)

Mağaza onaylı siparişte PG'ye korelasyon anahtarı + `userHandle` + `cardHandle` + tutar + alıcı bilgisi
ile çekim ister. PG sağlayıcıda saklı kartla (cardUserKey+cardToken) **NON-3D tek çekim** yapar (3DS
ekranı yok, taksit yok). Sonuç: başarı/başarısız/belirsiz + sağlayıcı ödeme kimliği (pgPaymentId). Aynı
korelasyon anahtarıyla ikinci istek çift çekim yapmaz (idempotent).

**Why this priority**: Saklı kartın ödeme değeri. Mevcut saklı-kartla çekim (033) korunur; değişen =
girdi kimliği (vault-token → userHandle+cardHandle) + taksit kaldırma + NON-3D.

**Independent Test**: Kayıtlı kartla çekim başarılı, pgPaymentId döner; aynı korelasyon anahtarıyla tekrar
→ yeni çekim yok, aynı sonuç; 3DS/OTP ekranı görünmez.

### User Story 4 - Kayıtlı Kart Silme (Priority: P2)

Mağaza `userHandle` + `cardHandle` ile kart siler; PG sağlayıcıdan yalnız o cardUserKey'in kartını
kaldırır. Yanıt: silindi/silinmedi. Başka kullanıcının kart kümesine erişilemez.

**Why this priority**: Kart yönetimi bütünlüğü; MVP sonrası.

**Independent Test**: Kart silinir, sonraki listede yok; başka userHandle'ın cardHandle'ıyla silme
reddedilir/etkisiz.

### User Story 5 - Eski PAN-POST Yolu Sökümü (Priority: P3)

PG'nin eski PAN-alan ucu (`TokenizeCard`: mağazadan kart numarası POST) kaldırılır; kart girişi yalnız
hosted form ile. Eski tek-token dış sözleşmesi bu feature ile 075 şekline geçtiğinden geriye-uyum
gerekmez (mağaza da eski yolu söktü).

**Why this priority**: Uyum temizliği; işlevsel yeniyi engellemez, en sonda.

**Independent Test**: PAN kabul eden uç artık yok; kart girişi yalnızca hosted-form üzerinden mümkün.

### Edge Cases

- Sağlayıcı erişilemez/timeout (oturum/liste/çekim): çekimde **belirsiz** (ambiguous) döner — mağaza
  geçici sayıp yeniden dener; listede "getirilemiyor" (bayat kopya gösterilmez).
- Kullanıcı hosted formu iptal eder / süre dolar: oturum tamamlanmaz, kalıcı kayıt yok.
- Aynı conversationId ile ikinci tamamlama: oturum tek-kullanımlık, tekrar işlenmez.
- Aynı kullanıcı ikinci kart ekler: sağlayıcı AYNI cardUserKey'e ekler (yeni kullanıcı-kimliği üretmez).
- Geçersiz/yabancı cardHandle ile çekim/silme: reddedilir (handle-scoped).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: PG, hosted kart-ekleme oturumu açan bir uç sunmalı: merchant + conversationId + callbackUrl
  girdisiyle sağlayıcının hosted formunu başlatıp `addUrl` + conversationId dönmeli. PAN bu istekte YOK.
- **FR-002**: PG, oturum sonucunu (callback ya da sorgu) sağlayıcıdan çekip başarıda `cardUserKey`'i
  (pgUserHandle) dönmeli; iptal/hata'da kalıcı kayıt yapmamalı.
- **FR-003**: PG, `userHandle` ile kayıtlı kartları canlı listelemeli: marka, son4, son-kullanma, alias,
  opak `cardHandle`. PAN/CVV asla dönmemeli; handle yoksa boş liste.
- **FR-004**: PG, `userHandle` + `cardHandle` ile kartı sağlayıcıdan silmeli; yalnız o kullanıcı-kümesi.
- **FR-005**: PG, `userHandle` + `cardHandle` + tutar + alıcı ile NON-3D tek çekim yapmalı (3DS yok,
  taksit yok); sonuç başarı/başarısız/belirsiz + pgPaymentId.
- **FR-006**: Çekim, korelasyon anahtarıyla idempotent olmalı (aynı anahtar → çift çekim yok).
- **FR-007**: Kart numarası (PAN) / CVV, PG sınırından içeri hiç girmemeli; yalnız sağlayıcının hosted
  formunda toplanır ve sağlayıcının kasasında yaşar.
- **FR-008**: Kart uçları (`/vault/card-sessions`, `/vault/cards`) OpenIddict merchant token'ıyla korunur
  — scope `cards.write` + `merchant_id` claim'i (store `PgCardClient` Bearer taşır; İLKE V claim-tabanlı
  tenant). Merchant path'te DEĞİL, claim'den çözülür. Charge ucu (`merchants/{merchantId}/payments`)
  mevcut **039 X-Api-Key** mekanizmasını korur. "Varsayılan açık" uç bırakılmaz.
- **FR-012**: Multitenant + kullanıcı izolasyonu (İLKE V, ZORUNLU) — list/delete/charge işlemleri yalnız
  çağıran merchant'a ait `userHandle` (cardUserKey) üzerinde geçerli olmalı; başka merchant'ın/kullanıcının
  kart kümesine erişim fail-closed reddedilir (yabancı userHandle/cardHandle → red).
- **FR-009**: Hosted-form oturumu tek-kullanımlık + süre-sınırlı korelasyon olmalı (conversationId).
- **FR-010**: Eski PAN-alan uç (`TokenizeCard`) kaldırılmalı; kart girişi yalnız hosted form.
- **FR-011**: Dış yüzey `pg-card-contract.md` ile birebir olmalı (mağaza 075 tüketicisi değişmeden çalışır).
- **FR-013**: Liste tek gerçek-kaynağı sağlayıcı (canlı); yerel `StoredCard` yalnız gösterim/denetim + eşleme
  (drift'te sağlayıcı kazanır) — (A1).

### Key Entities *(include if feature involves data)*

- **CardSession**: hosted-form korelasyonu — conversationId (kimlik), merchantId, cardUserKey (tamamlanınca),
  durum (Pending/Completed/Failed), oluşturma zamanı. Tek-kullanımlık.
- **StoredCard (mevcut, uyarlanır)**: sağlayıcı kimlikleri (cardUserKey/cardToken) + gösterim (marka/son4/
  bin/alias). PAN yok. 075 yüzeyinde `cardHandle`=cardToken, `userHandle`=cardUserKey olarak yansır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kart ekleme/listeleme/çekim akışlarında PAN/CVV PG kayıtlarında ve loglarında **0 kez** görünür.
- **SC-002**: Mağaza 075 tüketicisi (PgCardClient) PG'ye karşı değişiklik olmadan çalışır (sözleşme birebir).
- **SC-003**: Kullanıcı hosted formda kart kaydettikten sonra kart, sonraki listede marka+son4 ile görünür.
- **SC-004**: Aynı korelasyon anahtarıyla iki çekim isteği tek ödeme üretir (çift çekim yok).
- **SC-005**: Bir kullanıcının handle'ıyla başka kullanıcının kartı listelenemez/silinemez/çekilemez.
- **SC-006**: PAN kabul eden uç kalmaz (kart girişi yalnız hosted form).

## Assumptions

- Sağlayıcı = iyzico; Checkout Form (kart-kaydetme) + Card Storage (list/delete) + saklı-kartla NON-3D
  Payment yetenekleri sandbox'ta kullanılabilir. Mevcut iyzico motoru (022/032) + StoredCards üstüne kurulur.
- Auth sandbox X-Api-Key yeterli; tam OAuth/merchant-onboarding ayrı fasıl (029/030 + store 070).
- Hosted form nominal doğrulama tutarı: sandbox'ta sabit minimal tutar (ör. 1.00 TRY), para hareketi
  doğurmaz, iade gerekmez. Canlı ortamda küçük-doğrula+iade politikası ayrı fasıl (backlog) — sandbox
  kapsamı bu feature için yeterli (U1).
- Varsayılan-kart tercihi mağaza tarafında tutulur (PG'de yok).
- **Çok-kart gruplama (D1):** kullanıcı-başına tek cardUserKey, mağazanın add-session'a mevcut `userHandle`'ı
  göndermesine bağlı (contract additive alan). Mağaza `Wallet.PgUserHandle` gönderme eklemesi **store tarafı
  küçük iş** (bu repo kapsam-dışı). Gönderilmezse: her ekleme yeni cardUserKey → tek-kart güvenli çalışır,
  çok-kart tek listede görünmez. MVP tek-kart; çok-kart store eklemesiyle tamamlanır.

## Dependencies

- Store sözleşmesi: `ECommerceWithAgentFramework/specs/075-iyzico-card-storage/contracts/pg-card-contract.md`.
- Mevcut PG: iyzico kart altyapısı (032-iyzico-card-storage, 033-saved-card-payment), StoredCards domain,
  039 X-Api-Key charge/retrieve yüzeyi.