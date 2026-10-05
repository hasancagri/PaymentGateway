# Quickstart: MerchantId Görünürlük (PG bacağı)

Phase 1. Canlı doğrulama senaryoları. Domain davranışı xUnit'te (test-first); uçtan-uca PG+store Aspire ile.

## Önkoşul

- Aspire AppHost ayakta (Postgres + RabbitMQ + Merchant.Api + Identity.Server).
- `OnboardingCallbackOptions` dev secret'ları set: `BootstrapRegistrationKey` + `CallbackSecret`
  (`dotnet user-secrets set OnboardingCallback:BootstrapRegistrationKey/CallbackSecret --project src/services/Merchant.Api`).
- Store (ECommerceAgent) callback alıcısı ayakta + aynı `CallbackSecret` + m2m `ecommerce-onboarding` client.

## Senaryo 1 — Register → Pending (kabul makbuzu, credential yok)

1. Store `POST /api/v1/onboarding/register` (Bearer m2m + `X-Registration-Key` + correlationId + callbackUrl + business).
2. **Beklenen:** `202 { accepted, correlationId, status:"Pending" }`; `RegisterRequest` Pending + CorrelationId+CallbackUrl saklı; yanıtta MerchantId/Key YOK.

## Senaryo 2 — Negatif güvenlik (bootstrap key)

1. `X-Registration-Key` eksik/yanlış ile register.
2. **Beklenen:** `401/403`; başvuru yaratılmaz (fail-closed).
3. Aynı correlationId ikinci kez → `409` idempotent (çift yaratmaz).

## Senaryo 3 — Onay → HMAC-callback (makine teslim)

1. Admin MCP `admin_approve_registration(requestId)`.
2. **Beklenen:** Merchant Active; approve dönüşü `{ RequestId, Status, MerchantId }` — **MerchantKey YOK**.
3. PG store callbackUrl'ine POST eder: `X-Signature` hex + gövde `{ correlationId, merchantId, merchantKey, status:"Active" }`.
4. Store imza+correlation doğrular → persist. PG `200` alır (log: "callback teslim edildi", key GÖRÜNMEZ).

## Senaryo 4 — Callback dayanıklılık

1. Store ucu geçici kapalıyken onayla.
2. **Beklenen:** PG `EnsureSuccessStatusCode` hata → Wolverine durable retry; store açılınca teslim edilir; credential kaybolmaz, log/trace'e düşmez.

## Senaryo 5 — Reissue ekransız

1. Store `POST /api/v1/onboarding/reissue` (+correlationId+callbackUrl).
2. **Beklenen:** yeni key callback'le store'a gider (echo correlationId); S2S dönüşünde RevealUrl/key YOK; eski key anında ölür (`MerchantKeyReissued` → Identity/Payment).

## Senaryo 6 — İnsan-yüzey söküm (direk A)

1. Eski route'lar: `GET /onboarding/reveal/{token}`, `GET /onboarding/{token}` (form).
2. **Beklenen:** `404` (yüzey yok). `admin_resend_credential_link` tool listesinde YOK.

## Senaryo 7 — MCP/query hassas-alan (direk B)

1. `admin_get_merchants` / `admin_get_merchant` + MerchantScoped `GetMerchant`.
2. **Beklenen:** dönüşler MerchantKey + iban + taxNumber + identityNumber İÇERMEZ (denetim: grep `MerchantKey`/`iban` hiçbir tool-dönüşü/ILogger'da).

## Senaryo 8 — Red (callback yok)

1. Admin MCP `admin_reject_registration(requestId, reason)`.
2. **Beklenen:** callback gönderilmez; store `GET /onboarding/status?email=` ile Rejected + reason öğrenir.

## CROSS-REPO notu

Senaryo 3-5 uçtan-uca store alıcısıyla koşulur (store bacağı bitti). Reissue callback simetrisi (Karar 5) store `ReceiveMerchantCredentials`'ın Active-merchant key-replace'i karşıladığını teyit gerektirir.