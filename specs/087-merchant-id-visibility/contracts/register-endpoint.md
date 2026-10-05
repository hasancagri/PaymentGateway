# Kontrat: Merchant Kayıt Ucu (store → PG) — PG tarafı

Yön: ECommerce (store) → PG (DropShop, Merchant.Api). Sanksiyonlu dış-S2S (HTTP; dış PSP).
Sunucu: `RegisterRequests/Features/Commands/SubmitRegistration` (YENİ) + endpoint-extension.
Store istemcisi: `PgOnboardingClient.RegisterAsync` (store bacağı, bitti). Ayna: store `contracts/register-request.md`.

## İstek

```
POST {PG}/api/v1/onboarding/register
Authorization: Bearer <m2m token>                 # ecommerce-onboarding (PG IdP, merchant.write) — taşıma kimliği
X-Registration-Key: <BootstrapRegistrationKey>    # kayıt-ucu yetkisi (ortak bootstrap sır, PG ops out-of-band verir)
Content-Type: application/json

{
  "correlationId": "<guid>",                       # store üretir; callback'le eşleşir
  "callbackUrl": "<CallbackBaseUrl>/internal/merchant-registration/callback",
  "business": {                                     # finansal dahil — yalnız S2S gövde, MCP arg DEĞİL
    "type": "<MerchantType>",
    "name": "string", "email": "string", "gsmNumber": "string", "address": "string",
    "iban": "string",                               # hassas; gövdede, loglanmaz
    "contactName": "string", "contactSurname": "string",
    "identityNumber": "string?", "taxOffice": "string?",
    "taxNumber": "string?", "legalCompanyTitle": "string?"
  }
}
```

## Yanıt (senkron — yalnız kabul makbuzu, credential DEĞİL)

```
202 Accepted
{ "accepted": true, "correlationId": "<guid>", "status": "Pending" }
```

- Credential (MerchantId/Key) bu yanıtta DÖNMEZ — onay asenkron (admin MCP), callback'le gelir.
- `401/403`: bootstrap key (`X-Registration-Key`) geçersiz/eksik → store fail-closed (kayıt başlamaz).
- `400`: business doğrulama hatası (alan/biçim/tip-uyum — `RegisterRequest.Submit` kuralları; Result + resource kodu).
- `409`: aynı `correlationId`'li Pending başvuru (ya da mükerrer-email) → idempotent kabul, çift yaratmaz.

## Kurallar

- `iban` + `business.*` finansal/PII yalnız bu gövdede; MCP tool arg'ına, log/trace'e, tool dönüşüne GİRMEZ (direk B).
- `X-Registration-Key` yalnız bu uçta; başka PG ucu kabul etmez (dar kapsam, İlke V açık yetki).
- Kayıt `RegisterRequest`'i Pending yaratır + `CorrelationId` + `CallbackUrl` saklar (onayda okunur).