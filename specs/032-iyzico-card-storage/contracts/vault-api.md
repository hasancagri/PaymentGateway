# Contract: Vault Uçları (Payment.Api) — DIŞ SÖZLEŞME 031 İLE AYNI

Tüketici: ECommerce `GatewayCardTokenizer` (CANLI, değişmez). **Route/gövde/yanıt 031 ile birebir**
(FR-008 sıfır dokunuş). Değişen yalnız gateway'in İÇİ (AES-sakla → iyzico Saklı Kart proxy).

**Auth**: Bearer client_credentials — `cards.write` + `MerchantScoped` (yalnız Active merchant).

## `POST /api/v1.0/merchants/{merchantId}/vault/cards` — Tokenize (iyzico proxy)

Gövde (031 ile aynı; CVC yok):
```json
{ "pan": "5528790000000008", "expiry": "12/30", "holderName": "CARD HOLDER" }
```
**İç akış (yeni)**: gateway → iyzico `POST /cardstorage/card` (CardInformation) → iyzico
`cardUserKey`+`cardToken`+`binNumber`+`lastFourDigits`+`cardAssociation` döner → gateway saklar.
**200**: `{ "token": "card_..." }` — 031 ile AYNI (yalnız opak token; iyzico kimlikleri gateway'de kalır).
**400**: iyzico reddi (biçim/erişim) → `INVALID_OPERATION_ERROR` / alan hatası; kayıt oluşmaz (fail-closed).
**403**: route merchantId ≠ claim (MerchantScoped).

## `DELETE /api/v1.0/merchants/{merchantId}/vault/cards/{token}` — Revoke (iyzico + yerel)

**İç akış (yeni)**: token'dan StoredCard bulunur → iyzico `DELETE /cardstorage/card`
(cardUserKey+cardToken) best-effort → yerel soft revoke.
**200**: `{ "token": "card_..." }`. Zaten Revoked → 200 (idempotent). iyzico silme hatası → yine 200
(fail-open; yerel iptal tamam).
**400**: bilinmeyen/başka-merchant token → `RECORD_NOT_FOUND` (sahiplik sızdırmaz).

## iyzico Saklı Kart uçları (dış — gateway → iyzico)

| İşlem | iyzico ucu | İstek | Yanıt |
|---|---|---|---|
| createCard | `POST /cardstorage/card` | `CreateCardRequest{Email, ExternalId, Card{CardNumber,ExpireMonth,ExpireYear,CardHolderName,CardAlias}}` | `Card{CardUserKey, CardToken, BinNumber, LastFourDigits, CardAssociation, CardType}` |
| deleteCard | `DELETE /cardstorage/card` | `DeleteCardRequest{CardUserKey, CardToken}` | `Card{Status}` |

Auth: `IYZWSv2` imza (HashGeneratorV2 — spike'la kanıtlı). BaseUrl sandbox.

## Değişmezlik kanıtı

Quickstart S3 ECommerce'in MEVCUT binary'siyle koşar — 031→032 geçişi kullanıcıya görünmez (SC-001).
