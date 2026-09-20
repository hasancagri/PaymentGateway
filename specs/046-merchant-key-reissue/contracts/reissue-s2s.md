# Contract — Reissue S2S REST (store → PG)

Store admin tetikler; store S2S PG'ye çağırır. Auth = ecommerce-onboarding m2m (045 deseni).
Merchant key ile DEĞİL (chicken-egg).

## POST `api/v1/onboarding/reissue`

**Auth**: Bearer m2m (ecommerce-onboarding). **Request**:
```json
{ "merchantId": "guid", "reason": "unuttum | sizinti-suphesi | ... (opsiyonel)" }
```
(FR-011: ileride ad/e-posta çözümlemeli varyant — D5. v1 = çıplak merchantId.)

**200 OK**:
```json
{ "revealUrl": "https://pg/onboarding/reveal/{token}", "expiresAt": "iso8601" }
```
Yanıt yeni key'i İÇERMEZ (FR-005) — yalnız tek-kullanımlık reveal URL + expiry.

**Hatalar** (Result, key değişmeden):
- merchant yok / Active değil → 404/409 (FR-009)
- eşzamanlı istek → tek geçerli key'de birleşir (FR-010)

## Etki (yan sözleşme)

Başarıda: eski MerchantKey ANINDA geçersiz (Payment.Api hash + Identity client_secret güncellenir);
önceki reveal linkleri ölür; bir `MerchantKeyReissueLog` yazılır.
