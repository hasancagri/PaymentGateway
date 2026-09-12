# Phase 1 Data Model: Hosted Kart Saklama (075)

Kapsam = Payment BC (paymentDb). PAN/CVV hiçbir varlıkta yok — sağlayıcı (iyzico) kasasında.

## Aggregate: CardSession (YENİ) — paymentDb

Hosted-form dönüşünü mağaza kullanıcısına bağlayan tek-kullanımlık korelasyon (R3).

| Alan | Tip | Not |
|------|-----|-----|
| `Id` (=conversationId) | Guid | Mağazadan gelen conversationId; iyzico'ya da gider |
| `MerchantId` | Guid | X-Api-Key'den çözülen merchant (izolasyon) |
| `CheckoutFormToken` | string? | iyzico CF token (retrieve için) |
| `CardUserKey` | string? | CF retrieve sonrası (=pgUserHandle); başlangıçta null |
| `Status` | enum | Pending / Completed / Failed |
| `CreatedAt` | DateTimeOffset | süre-sınırı |

### Davranış (test-first — İlke VI)

| Metot | Döner | Kural |
|-------|-------|-------|
| `Start(merchantId, conversationId, cfToken, now)` | CardSession | Pending doğar |
| `Complete(cardUserKey)` | ResultDomain | cardUserKey boş red; Pending→Completed (tek sefer) |
| `Fail()` | ResultDomain | Pending→Failed (iptal/hata; kalıcı kart kaydı yok) |
| `IsUsable(now, ttl)` | bool | Pending + süre dolmamış |

## Aggregate: StoredCard (MEVCUT — uyarlanır) — paymentDb

032'de kuruldu (cardUserKey/cardToken + gösterim). 040 değişiklikleri:

| Alan | Değişim |
|------|---------|
| `CardUserKey` | Anlam: **per-kart → per-USER** (R2 gruplama). Aynı kullanıcının kartları aynı değer |
| `CardToken` | = 075 `cardHandle` (opak, sil/çekim referansı) |
| `Bin`/`Last4`/`Brand`/`Expiry`/`HolderName` | gösterim — KALIR |
| `Token` (opak) | İç kayıt kimliği KALIR; 075 dış yüzeyde cardHandle=CardToken kullanılır |
| oluşturma | `TokenizeCard` (PAN) yerine `CompleteCardSession` (CF retrieve) yazar |

> Not: liste canlı iyzico'dan (R4); StoredCard denetim/gösterim + eşleme için tutulur. Silme handle ile
> (iyzico delete) — yerel kayıt soft-revoke edilebilir.

## Çalışma-anı izdüşümü (kalıcı DEĞİL): CardView

iyzico Card list yanıtından türetilir; response DTO.

| Alan | Kaynak | Mağazaya |
|------|--------|----------|
| `cardHandle` | iyzico cardToken | Evet |
| `brand` | CardAssociation eşlemesi | Evet |
| `last4` | lastFourDigits | Evet |
| `expiryMonth/Year` | expireMonth/Year | Evet |
| `alias` | cardAlias | Evet |
| PAN / CVV | — | **ASLA** |

## Ödeme girdisi (çekim)

`ChargePayment` girdisi vault-token → **cardUserKey + cardToken** (+ price, buyer, correlationKey);
NON-3D, taksitsiz. Yanıt: status + pgPaymentId. İdempotency: correlationKey.

## Silinen/değişen

- **Silinir:** `TokenizeCard` (PAN-POST) + token-bazlı `RevokeCard` (R6).
- **Eklenir:** `CardSession` aggregate + Checkout Form init/retrieve + Card list + handle-bazlı delete.
- **Değişir:** `StoredCard.CardUserKey` per-user; `ChargePayment` girdisi handle + NON-3D taksitsiz.
- **Migrasyon:** yok (sandbox/demo — kartlar yeniden eklenir).