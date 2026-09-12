# Phase 0 Research: Hosted Kart Saklama (075 Hizalama)

Karar / Gerekçe / Elenen. Sağlayıcı = iyzico; iyzico'nun kendi mekaniği (Checkout Form, Card Storage,
NON-3D Payment) PG'nin içinde. Bu belge PG↔iyzico + PG↔mağaza tarafını çözer.

## R1 — Hosted kart-ekleme = iyzico Checkout Form (kart-kaydet)

**Decision:** "kart ekle" → PG iyzico **Checkout Form initialize** çağırır (kart-kaydetme açık, nominal
doğrulama tutarı), `paymentPageUrl`'i (=addUrl) mağazaya döner. Kullanıcı PAN'ı yalnız iyzico sayfasında
girer. Form bitince PG **Checkout Form retrieve** ile `cardUserKey`(+`cardToken`) alır.
**Rationale:** iyzico'da ödemesiz saf hosted tokenizasyon yok; PAN toplama Checkout Form ile (tutar ister)
→ nominal doğrulama (sandbox'ta para hareketi yok). PAN mağaza/PG sınırına girmez (075 uyum çekirdeği).
**Alternatives:** PAN'ı mağaza→PG POST (mevcut `TokenizeCard`) — 075 bunu söktü (uyum ihlali); RED.

## R2 — cardUserKey kullanıcı-başına (gruplama)

**Decision:** Kullanıcı başına TEK `cardUserKey`; kartlar ona eklenir. add-session isteği **opsiyonel
mevcut userHandle** taşır (additive alan): doluysa iyzico o cardUserKey'e kart ekler, boşsa yeni üretir.
Mağaza `Wallet.PgUserHandle` doluysa gönderir.
**Rationale:** "userHandle ile listele/sil/çek" tek cardUserKey ister. 032 per-kart cardUserKey ertelemişti
(R2); 040 gruplamayı açar. Contract additive — eski çağrı (userHandle'sız ilk kart) kırılmaz.
**Alternatives:** merchant+stableUserId ile PG-içi eşleme — add-session'da kullanıcı kimliği yok (yalnız
conversationId); mağaza kimliği taşımıyor → uygulanamaz. Per-kart cardUserKey (032) — liste userHandle'la
çalışmaz; RED.

## R3 — Oturum ↔ dönüş korelasyonu + auth

**Decision:** `StartCardSession` merchant + `conversationId` + `callbackUrl`'i tek-kullanımlık `CardSession`
(paymentDb) + iyzico CF token'ına bağlar. Dönüş (callback ya da mağaza pull `GET .../{conversationId}`)
**JWT beklemez**; conversationId ile oturum bulunur, iyzico retrieve edilir, sonuç yazılır. Kart uçları
**X-Api-Key** (merchant) ile korunur (mevcut 039 deseni).
**Rationale:** dış dönüş makine çağrısıdır; tahmin-edilemez oturum + iyzico doğrulaması güvenler. Tam OAuth
ertelenmiş (sandbox).
**Alternatives:** callback'i user JWT ile korumak — dış çağrı token taşımaz; imkânsız.

## R4 — Kart listeleme (canlı, iyzico'dan)

**Decision:** `ListCards(userHandle)` → iyzico **Card list** (cardUserKey) → `{cardHandle=cardToken, brand,
last4, expiryMonth/Year, alias}`. Yerel `StoredCard` gösterim için tutulur ama liste canlı iyzico'dan
(tek gerçek-kaynak). Handle yoksa boş liste; iyzico erişilemezse "getirilemiyor".
**Rationale:** 075 FR-003/SC-003 canlı doğruluk. **Alternatives:** yalnız yerel StoredCard listesi — iyzico
ile drift; RED.

## R5 — Silme + NON-3D çekim (handle ile)

**Decision:** Silme = iyzico **Card delete**(cardUserKey, cardToken); yalnız o kullanıcı-kümesi. Çekim =
iyzico **Payment** (paymentCard{cardUserKey, cardToken}), **NON-3D + taksitsiz**. Sonuç success/failure/
ambiguous + pgPaymentId. Çekim idempotent (correlationKey).
**Rationale:** 033 saklı-kartla çekim zaten var; değişen girdi kimliği (vault-token→handle) + taksit
kaldırma + NON-3D. **Alternatives:** 3DS'li — 075 NON-3D + agent onayı seçti; RED.

## R6 — Eski PAN-POST sökümü

**Decision:** `TokenizeCard` (PAN mağaza→PG POST) + token-bazlı `RevokeCard` kaldırılır; kart girişi yalnız
hosted form, silme handle ile. Geriye-uyum yok (mağaza da eski yolu söktü, 075).
**Rationale:** SC-006 (PAN kabul eden uç kalmaz). **Alternatives:** ikisini tut — kullanıcı reddetti (uyum
borcu sürer); RED.

## Çözülen bilinmeyenler özeti

| # | Konu | Karar |
|---|------|-------|
| R1 | Hosted ekleme | iyzico Checkout Form (kart-kaydet, nominal) |
| R2 | cardUserKey gruplama | kullanıcı-başına tek; add-session opsiyonel userHandle (additive) |
| R3 | Dönüş auth | conversationId + tek-kullanımlık oturum; uçlar X-Api-Key |
| R4 | Listeleme | canlı iyzico Card list; handle yoksa boş |
| R5 | Sil/çek | iyzico delete + NON-3D taksitsiz payment; charge idempotent |
| R6 | Eski PAN yolu | TokenizeCard + token-RevokeCard SÖKÜLÜR |