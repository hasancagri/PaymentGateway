# Kontrat: Credential Callback (PG → store) — PG tarafı (gönderici)

Yön: PG (DropShop, Merchant.Api) → ECommerce (store). Dış-webhook (HTTP; İlke I sanksiyonlu istisna).
Gönderici: `Merchants/Features/Commands/DeliverCredentialCallback` (YENİ) — 041 `StoreCallbackDelivery` aynası.
Alıcı: store `MerchantRegistrationCallbackEndpointExtension` + `ReceiveMerchantCredentials` (store bacağı, bitti).
Ayna: store `contracts/credential-callback.md`.

## İstek (PG gönderir)

```
POST {callbackUrl}                                 # store register/reissue'da bildirdi
X-Signature: <hex(HMAC-SHA256(CallbackSecret, raw_body))>   # lowercase hex, ham gövde üstünde
Content-Type: application/json

{
  "correlationId": "<guid>",     # register/reissue'daki ile eşleşir (store doğrular)
  "merchantId": "<guid>",        # PG onayda bastı
  "merchantKey": "<secret>",     # per-merchant sır; ASLA log/trace/tool-dönüşünde
  "status": "Active"
}
```

## Yanıt (store döner, PG yorumlar)

```
200 OK      # store imza+correlation doğruladı, persist etti (veya idempotent no-op)
4xx/5xx     # PG için HATA → exception → Wolverine durable retry (kayıpsız yeniden gönderim)
```

- PG `EnsureSuccessStatusCode`: 2xx dışı her yanıt → exception → durable retry (041 FR-009 emsali).
- Teslim `[Transactional]` outbox ile publish edilir (yayın yalnız DB commit'te gider — onay/reissue atomik).

## Tetikleyiciler

- **Onay:** `AdminApproveRegistration` (admin MCP) → `RegisterRequest.CallbackUrl` + `CorrelationId` okunur → `Deliver(callbackUrl, correlationId, merchantId, merchantKey, "Active")` publish.
- **Reissue:** `ReissueMerchantKey` (store S2S, request'te correlationId+callbackUrl) → yeni key aynı `Deliver` ile (echo correlationId) publish.
- **Red:** callback YOK — store `GET /onboarding/status?email=` ile öğrenir.

## Kurallar

- İmza ham gövde (serialize edilen tam JSON) üstünde HMAC-SHA256(`CallbackSecret`) hex; store aynı ham body'yi doğrular.
- `merchantKey` yalnız imzalı gövdede; hiçbir log/trace/tool-dönüşüne yazılmaz (direk B / ADR nöbetçi).
- `CallbackSecret` ≠ `BootstrapRegistrationKey` ≠ `MerchantKey` (üç ayrı sır, ayrı ömür).
- İdempotent: aynı correlationId için retry güvenli (store çift-callback'i no-op yutar).