# Contract: Vault Uçları (Payment.Api) — DIŞ SÖZLEŞME SABİT

Tüketici: ECommerce `GatewayCardTokenizer` (CANLI — kaynak okumasıyla doğrulandı, R4).
Bu sözleşme DEĞİŞTİRİLEMEZ; ECommerce'e sıfır dokunuş FR-009.

**Auth**: Bearer client_credentials — scope `cards.write` + policy `MerchantScoped`
(route `{merchantId}` = token `merchant_id` claim'i; yalnız Active merchant `cards.write` alır —
mevcut Identity zinciri).

## `POST /api/v1.0/merchants/{merchantId}/vault/cards` — Tokenize

Gövde (CVV alanı YOK — FR-002):

```json
{ "pan": "4111111111111111", "expiry": "12/29", "holderName": "CARD HOLDER" }
```

**200**: `{ "token": "card_9f2c..." }` — YALNIZ token (PAN/bin/last4/brand dönmez; istemci
gösterim alanlarını lokal türetir).
**400**: `FeatureObjectResultModel` zarfı — `VALUE_IS_REQUIRED` (eksik alan),
`INVALID_FORMAT` (Luhn/hane/expiry). İstemci !2xx'i fail-closed sayar (kart kaydolmaz).
**403**: route merchantId ≠ token claim (MerchantScoped fail-closed).

## `DELETE /api/v1.0/merchants/{merchantId}/vault/cards/{token}` — Revoke

**200**: `{ "token": "card_..." }`. Zaten Revoked → yine 200 (idempotent — FR-007).
**400**: bilinmeyen token VEYA başka merchant'ın token'ı → `RECORD_NOT_FOUND` (sahiplik sızdırmaz:
"var ama senin değil" ayrımı yapılmaz). İstemci fail-open (yerel silme zaten tamam).

## Değişmezlik testi

Canlı doğrulama (quickstart S3) ECommerce'in MEVCUT binary'siyle koşar — sözleşme uyumu ancak
öyle kanıtlanır; curl testleri (S1/S2) şekli birebir bu dosyadan alır.
