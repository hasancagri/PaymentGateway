# Implementation Plan: Yapısal İdempotent Çekim + Retrieve Yüzeyi

**Branch**: `039-structural-charge-retrieve` | **Spec**: [spec.md](./spec.md)

## Özet

Order.Api'nin tükettiği server-to-server REST yüzeyi: idempotent çekim (`correlationKey`) +
retrieve (key/id). Mevcut `ChargePayment` slice'ı **repurpose** edilir (başka çağıranı yok); yeni
retrieve query slice'ı eklenir. Auth: **X-Api-Key** (merchant key = mevcut MerchantKey, hash'li).

## Teknik kararlar

- **İdempotency (Option A — Charging marker):** iyzico çağrısından ÖNCE `Charging` durumunda Payment
  kaydı persist edilir (`correlationKey` + unique partial index). Retry marker'ı bulur, tekrar çekmez.
  - load-by-key: Success/Failed → var olanı dön (çekim yok); Charging → `pending` dön (Order reconcile).
  - Yarış: eşzamanlı iki insert → unique index ihlali → yakala → reload → var olanı dön (FR-003).
- **Unique index partial:** `correlation_key IS NOT NULL` — agent-path kayıtları (key'siz) çakışmaz.
- **Auth (Model 2 — per-merchant key):** `MerchantKey` (OAuth ClientSecret) ikili amaç: X-Api-Key.
  Payment.Api `MerchantApiKeyReference {Id=merchantId, KeyHash}` tutar (SHA-256, indeksli). Lifecycle
  event (`MerchantCreated`/`Provisioned`) key taşır → mevcut handler hash yazar (yeni ihraç yok).
  - `ApiKeyAuthenticationHandler` (scheme "ApiKey"): X-Api-Key → SHA-256 → lookup → `merchant_id` claim.
  - Mevcut `MerchantScoped` policy (claim==route) DEĞİŞMEDEN çalışır; scheme+policy Payment.Api-local.
- **Status wire lowercase:** ECom `Map()` `"success"`/`"failed"` bekler; Charging → `"pending"`.
- **Basket sentezi (FR-008):** istek basket taşımaz; tek sentetik kalem (IyzicoRequestOptions, price).
- **Active gate (FR-009):** `MerchantStatusReference` Active değilse fail-closed (agent-path deseni).
- **Currency:** Payment'a alan eklenmez; yanıtta config (IyzicoRequestOptions.Currency=TRY) echo.
- **paymentId = Payment.Id (Guid)** — retrieve-by-id bununla; ProviderPaymentId iz alanı.

## Aggregate değişiklikleri (Payment)

- +`CorrelationKey` (string?, private set), +`PaymentStatus.Charging = 3`.
- +`Begin(merchantId, vaultToken, correlationKey, price, paidPrice, installment)` → Charging.
- +`Succeed(providerPaymentId, commission, fee)` / +`Fail()` mutators (Charging→terminal; ResultDomain).
- Eski `Succeeded`/`Failed` static factory'ler agent-path için KALIR.

## Dosya planı

| Dosya | Değişim |
|-------|---------|
| `Domains/Payments/Payment.cs` | +CorrelationKey, +Charging, +Begin/Succeed/Fail |
| `Domains/Payments/Features/Commands/ChargePayment.cs` | repurpose: correlationKey, marker, sentez, lowercase |
| `Domains/Payments/Features/Queries/RetrievePayment.cs` | YENİ: by-key + by-id + GET endpoint'leri |
| `Domains/Payments/PaymentEndpointExtension.cs` | RetrievePayment endpoint'lerini map'le |
| `Domains/MerchantStatus/MerchantApiKeyReference.cs` | YENİ doc (merchantId + KeyHash) |
| `Domains/MerchantStatus/MerchantLifecycleEventHandler.cs` | Created/Provisioned'da KeyHash yaz |
| `Auth/ApiKeyAuthenticationHandler.cs` | YENİ: X-Api-Key → merchant_id claim |
| `Program.cs` | Marten CorrelationKey unique partial index; ApiKey scheme + policy |
| `tests/Payment.Api.Tests/PaymentTests.cs` | Begin/Succeed/Fail + idempotency kararı testleri |

## Anayasa kontrolü

- BC izolasyonu: Order PG'nin DB'sine değil API'sine erişir. ✅
- Zengin aggregate: idempotency/marker mantığı Payment'ta (Begin/Succeed/Fail). ✅
- VSA + CQRS: charge=command, retrieve=query ayrı slice. ✅
- Result pattern: mutators ResultDomain; endpoint IsSuccess→HTTP. ✅
- Domain-TDD: Payment mutators test-first. ✅
