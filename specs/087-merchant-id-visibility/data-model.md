# Data Model: MerchantId Görünürlük (PG bacağı)

Phase 1. merchantDb (Marten). Yalnız değişen/yeni/sökülen yapılar.

## RegisterRequest (aggregate — EXTEND)

Mevcut: Type, Name, Email, GsmNumber, Address, Iban, ContactName, ContactSurname, IdentityNumber?, TaxOffice?, TaxNumber?, LegalCompanyTitle?, Status (Pending/Approved/Rejected), RejectReason?, MerchantId? + audit.

| Alan | Tip | Not |
|---|---|---|
| CorrelationId | Guid | **YENİ** — store üretir, callback eşleme; register'da set |
| CallbackUrl | string | **YENİ** — PG→store teslim hedefi; register'da set, onayda okunur |

**Davranış (test-first, İlke VI):**
- `Submit(...)` EXTEND: mevcut alan doğrulamaları + `correlationId` + `callbackUrl` parametreleri; ikisi de boş olamaz (callbackUrl mutlak URL, correlationId non-empty Guid). Geçerse Pending doğar (CorrelationId+CallbackUrl set).
- `Approve(merchantId)` DEĞİŞMEZ (zaten MerchantId bağlar, Approved terminal).
- Finansal/PII alanlar (Iban/TaxNumber/IdentityNumber/Email/GsmNumber) yalnız bu aggregate'te + register S2S gövdesinde; tool/query dönüşüne girmez (direk B).

**İdempotency (handler):** register'da aynı `CorrelationId`'li Pending başvuru varsa → yeni kayıt YARATMA, `409` idempotent kabul (çift başvuru guard). Mevcut mükerrer-email kuralı korunur (cross-document sorgu, handler'da).

## Merchant (aggregate — DEĞİŞMEZ)

MerchantId (Guid, AggregateRoot.Id) + MerchantKey ("mk_"+Guid, onayda basar) + statü (Active/Passive/Suspended) + iyzico-SubMerchant hizalı alanlar. `Create`/`ReissueKey`/`ChangeStatus` dokunulmaz. CallbackUrl burada SAKLANMAZ (onboarding-transport kaygısı sızmaz — Karar 4/5).

## Options — OnboardingCallbackOptions (YENİ)

`IConfiguration` doğrudan okuma YOK; Options pattern (BindConfiguration + ValidateDataAnnotations + ValidateOnStart).

| Alan | Rol | Ömür |
|---|---|---|
| BootstrapRegistrationKey | register ucu `X-Registration-Key` doğrulaması | ortak, platform-seviyesi, rotate |
| CallbackSecret | PG→store callback HMAC imzası | ayrı sır |

(Mevcut `Onboarding` options'ın reveal alanları — RevealLinkLifetime/PublicBaseUrl — reveal söküldükçe temizlenir.)

## Callback mesajı — DeliverCredentialCallback (YENİ, Wolverine)

041 `StoreCallbackDelivery` aynası. Domains/Merchants/Features/Commands.

- `Deliver(string CallbackUrl, Guid CorrelationId, Guid MerchantId, string MerchantKey, string Status)` record.
- `DeliverHandler.Handle` (sınıf adı TEKİL `Handler`): ham JSON `{ correlationId, merchantId, merchantKey, status }` → `X-Signature = HMAC-SHA256(CallbackSecret, raw_body)` hex → POST CallbackUrl → `EnsureSuccessStatusCode` (2xx dışı → Wolverine durable retry).
- Publish: approve + reissue `[Transactional]` handler'larından `IMessageBus.PublishAsync` (outbox — yalnız DB commit'te gider). `Program.cs` `IncludeType` gerekirse (ad `*Handler` ise gerekmez).
- `MerchantKey` asla log/trace'e yazılmaz (yalnız imzalı gövde).

## SİLİNEN yapılar (TEARDOWN — direk A)

- `Domains/CredentialRevealLinks/` tümü: `CredentialRevealLinkEndpointExtension` (`GET /onboarding/reveal/{token}`), `CredentialRevealLink` aggregate, `RevealCredentials` command.
- `Domains/OnboardingFormSessions/` tümü: endpoint (`GET/POST /onboarding/{token}`), aggregate, `CreateFormSession`, `Pages/` form HTML + `.csproj` embedded-resource.
- `AdminResendCredentialLink` command + MCP tool + `resend_credential_link` sabitleri (McpToolNames/Descriptions) + `MerchantAdminSurface.ToolScopeMap` girdisi.
- Approve `SendEmailRequested` bloğu TÜMDEN kaldırılır (mail'le hiç link gitmez — karar 2026-10-05; store credential'ı callback'ten öğrenir, bildirim gereksiz).
- `ReissueMerchantKey` `RevealUrl` dönüşü + reveal-link öldürme bloğu.

## İlişki / akış özeti

**Register→onay:** store `POST /api/v1/onboarding/register` (m2m + X-Registration-Key) → `SubmitRegistration` handler → `RegisterRequest.Submit(... correlationId, callbackUrl)` persist Pending → `202`. Admin MCP `admin_approve_registration` → `Merchant.Create` + `RegisterRequest.Approve` + `MerchantCreated` event + `DeliverCredentialCallback.Deliver(request.CallbackUrl, request.CorrelationId, merchant.Id, merchant.MerchantKey, "Active")` publish. **Red:** `admin_reject_registration` → callback yok; store `GET /onboarding/status?email=` ile öğrenir.

**Reissue:** store `POST /api/v1/onboarding/reissue` (+correlationId+callbackUrl) → `ReissueMerchantKey` → `merchant.ReissueKey()` + `MerchantKeyReissued` event + log + `DeliverCredentialCallback` publish (echo correlationId). Dönüş credential/RevealUrl-free (kabul makbuzu).