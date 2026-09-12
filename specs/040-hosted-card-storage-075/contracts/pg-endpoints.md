# Contract: PG Kart Uçları (mağaza → PG) + iyzico Eşleme

Dış yüzey `ECommerceWithAgentFramework/specs/075-iyzico-card-storage/contracts/pg-card-contract.md` ile
**birebir**. Auth = X-Api-Key (merchant; sandbox). Sağdaki "iyzico" = PG-içi çağrı (kapsam-dışı impl).

## POST /api/v1/vault/card-sessions  (add-session)

- **İstek:** `{ merchantId, conversationId, callbackUrl, userHandle? }` — `userHandle?` additive (R2):
  varsa iyzico o cardUserKey'e ekler.
- **Yanıt:** `{ addUrl, conversationId }` — `addUrl` = iyzico Checkout Form paymentPageUrl.
- **iyzico:** Checkout Form **initialize** (kart-kaydet açık, nominal tutar, callbackUrl). PAN yok.
- **Etki:** `CardSession(Pending)` yazılır (cfToken saklanır).

## GET /api/v1/vault/card-sessions/{conversationId}  (complete/pull)  [+ opsiyonel POST callback]

- **Yanıt (başarı):** `{ status:"success", pgUserHandle }` (=cardUserKey)
- **Yanıt (iptal/hata):** `{ status:"failure"|"cancelled" }` — kalıcı kart kaydı yok.
- **iyzico:** Checkout Form **retrieve** (cfToken) → cardUserKey + cardToken(lar). `StoredCard` yazılır,
  `CardSession.Complete(cardUserKey)`.

## GET /api/v1/vault/cards?userHandle=  (list)

- **Yanıt:** `{ cards:[{ cardHandle, brand, last4, expiryMonth, expiryYear, alias }] }` — PAN/CVV YOK.
- **iyzico:** Card **list** (cardUserKey). Handle yoksa `{cards:[]}`.

## DELETE /api/v1/vault/cards  (delete)

- **İstek:** `{ userHandle, cardHandle }`
- **Yanıt:** `{ deleted: bool }`
- **iyzico:** Card **delete** (cardUserKey, cardToken). Yalnız o kullanıcı-kümesi.

## POST /api/v1/merchants/{merchantId}/payments  (charge NON-3D — mevcut 039 yüzeyi evrilir)

- **İstek:** `{ correlationKey, userHandle, cardHandle, price, currency:"TRY", buyer{...} }`
  - ~~vaultToken~~ → `userHandle` + `cardHandle`; `installment` KALDIRILDI (tek çekim).
- **Yanıt:** `{ status:"success"|"failure"|"ambiguous", pgPaymentId }`
- **iyzico:** Payment (paymentCard{cardUserKey, cardToken}), **NON-3D** (3DS yok). Idempotent: correlationKey.

## Hata eşleme

iyzico hata → Payment resource sabitleri (`<Service>ResourceConstants`). Serbest metin yasak. Ağ/timeout
→ charge'da `ambiguous`; liste/oturum "getirilemiyor/işlenemiyor" (bayat kopya yok).

## SÖKÜLEN uçlar (R6)

- `POST /vault/cards` (TokenizeCard — PAN POST) ve `DELETE /vault/cards/{token}` (token-RevokeCard)
  kaldırılır. Kart girişi yalnız hosted form; silme handle ile.