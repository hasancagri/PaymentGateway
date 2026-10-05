# Research: MerchantId Görünürlük (PG bacağı)

Phase 0. NEEDS CLARIFICATION yok (3 karar specify clarify'da kapandı). Kararlar + mevcut-kod dayanakları.

## Karar 1 — Credential teslimi: makine-handoff, 041 StoreCallbackDelivery aynası

- **Decision:** PG onayda MerchantId+MerchantKey'i store callbackUrl'ine POST eder, `X-Signature = HMAC-SHA256(CallbackSecret, raw_body)` (lowercase hex). Teslim Wolverine `[Transactional]` outbox + durable retry (2xx dışı → exception → kayıpsız yeniden gönderim).
- **Rationale:** 041 `Payment.Api/.../StoreCallbackDelivery.cs`'te birebir emsal: `Deliver` record (CallbackUrl taşır) + `DeliverHandler.Handle` (ham JSON imzala+POST, `EnsureSuccessStatusCode` → durable retry). Sıfır yeni transport deseni. Store tarafı 077/087 `CallbackSignatureValidator` ile aynı ham-body + CallbackSecret'ı doğrular.
- **Alternatives:** Senkron yanıtta credential (onay asenkron admin MCP aksiyonu → düşer); custom retry (Wolverine durable zaten var → gereksiz).

## Karar 2 — Kayıt ucu auth: m2m bearer + ayrı X-Registration-Key

- **Decision:** Register REST ucu iki katman: mevcut `ecommerce-onboarding` m2m bearer (PG Identity.Server, `merchant.write` scope — taşıma kimliği) + ayrı `X-Registration-Key: <BootstrapRegistrationKey>` header (kayıt-ucu yetkisi, ortak bootstrap sır). Geçersiz bootstrap key → `401/403` fail-closed.
- **Rationale:** Tavuk-yumurta — kayıtta henüz MerchantKey yok. m2m token = genel S2S taşıma kimliği (zaten var, 045/046 `ecommerce-onboarding`); bootstrap key = yalnız bu ucu açan dar yetki (iki kaygı ayrı). Store kontratı (`register-request.md`) `X-Registration-Key`'i böyle tanımlıyor. İlke V: durum-değiştiren uç açık yetki beyan eder.
- **Alternatives:** Yalnız m2m token (kayıt-ucu özel yetkisini ayırmaz); per-merchant key (kayıtta yok → imkânsız); mTLS (dev için ağır).

## Karar 3 — Üç ayrı sır, ayrı Options

- **Decision:** `BootstrapRegistrationKey` (ortak, register ucu) ≠ `MerchantKey` (per-merchant, onayda basar, ödeme) ≠ `CallbackSecret` (callback HMAC). Bootstrap + CallbackSecret yeni `Options/OnboardingCallbackOptions` POCO'suna (BindConfiguration + ValidateDataAnnotations + ValidateOnStart). `IConfiguration` doğrudan okuma YOK.
- **Rationale:** Sızma yarıçapını ayırır; biri rotate edilince öteki etkilenmez. 041 `CallbackSecret` zaten MerchantKey'den ayrıydı — emsal. Dev user-secrets, prod vault (S5 borcuyla aynı).

## Karar 4 — Register→approve: callbackUrl RegisterRequest'te saklanır

- **Decision:** Register ucu `RegisterRequest`'i Pending yaratır + `CorrelationId` + `CallbackUrl` saklar. Onay (admin MCP, store DEĞİL tetikler) bu alanları RegisterRequest'ten okuyup `DeliverCredentialCallback` publish eder.
- **Rationale:** Onay store-başlatan değil admin-başlatan asenkron aksiyon → callbackUrl kayıt anında kalıcı olmalı (approve anında store bağlamı yok). `RegisterRequest.Approve` zaten MerchantId bağlıyor; correlation/callbackUrl aynı aggregate'in kayıt-yaşam-döngüsü verisi (onboarding-transport, Merchant kimliğine sızmaz).
- **Alternatives:** callbackUrl'i Merchant'ta saklamak (Merchant = iyzico-SubMerchant hizalı kimlik aggregate'i; transport kaygısı sızdırmak istemiyoruz).

## Karar 5 — Reissue→callback: S2S request pass-through (RegisterRequest lookup YOK)

- **Decision:** Reissue store-başlatan S2S (`POST /onboarding/reissue`); request'e `correlationId` + `callbackUrl` eklenir (register ile simetrik). PG yeni key'i doğrudan o callbackUrl'e `DeliverCredentialCallback` ile gönderir (echo correlationId); RevealUrl/reveal-link söküldü. `MerchantKeyReissued` event + salt-append log DEĞİŞMEZ.
- **Rationale:** Reissue store tetiklediği için callbackUrl request'te taşınır → RegisterRequest lookup/Merchant EXTEND gereksiz; uniform callback kontratı. Store plan reissue-callback mekanizmasını PG kararına bıraktı (store research "Açık kalan"). 
- **CROSS-REPO (teyit):** Store reissue başlatıcısı (`AdminReissueMerchantKey`) correlationId+callbackUrl SAĞLAMALI; store `ReceiveMerchantCredentials` Active-merchant key-replace'i correlation ile karşılamalı (store `UpdateKey` KEEP). Canlı E2E'den önce store tarafı bu simetri için doğrulanır.
- **Alternatives:** callbackUrl'i RegisterRequest'ten çöz + server-correlation üret (store'un correlation-match modeliyle çelişir); reveal koru (087 direk A ihlali).

## Karar 6 — Hassas-alan scrub + nöbetçi (direk B)

- **Decision:** MerchantScoped `GetMerchant` dönüşünden MerchantKey çıkarılır (FR-B2). Admin MCP query tool'ları (`AdminGetMerchants`/`AdminGetMerchant`) ZATEN credential+finansal/PII-free (044 FR-002/003) — değişmez, nöbetçiyle korunur. Yeni/değişen hiçbir dönüş DTO'su sır/PII taşımaz; grep denetimi (`MerchantKey`/`iban` hiçbir ILogger/tool-dönüşünde).
- **Rationale:** Direk B çoğunlukla 044'te sağlanmış; 087 boşluğu yalnız MerchantScoped self-read'in key döndürmesi (store artık callback'ten alır → gereksiz). Nöbetçi drifti önler.

## Karar 7 — Söküm kapsamı (direk A)

- **Decision:** TEARDOWN — `Domains/CredentialRevealLinks/` (endpoint + `CredentialRevealLink` aggregate + `RevealCredentials`), `Domains/OnboardingFormSessions/` (endpoint + aggregate + `CreateFormSession` + `Pages` form HTML), `AdminResendCredentialLink` (+ MCP tool, `resend_credential_link` sabitleri), approve-email reveal-URL gövdesi. `Program.cs` map + options kayıtları sökülür.
- **Rationale:** Sır artık ekrana hiç girmiyor → reveal/form/resend ölü + risk. Store formu açmıyor (S2S register'a geçti) → form öksüz, tek tüketici ECommerce. Kullanıcı "tam sök" dedi (clarify).
- **Dikkat:** `GetOnboardingApplicationStatus` KEEP (red'i store buradan öğrenir); `MerchantKeyReissueLog` + history query KEEP (reveal'e bağlı değil).

## Karar 8 — Onay-email tümden kalkar (link YOK)

- **Decision:** Approve `SendEmailRequested` bloğu tümden kaldırılır; mail'le hiç link/bildirim gitmez (karar kullanıcı, 2026-10-05). Store credential'ı callback'ten öğrenir → onay bildirimi gereksiz.
- **Rationale:** Direk A — mail'le link = sır-ekrana/dış-kanala sızma yüzeyi. Makine-handoff varken email konusuz. `SendEmailRequested` kontratı (Shared) durur; Merchant.Api onboarding'de artık emitter yok (başka BC kullanabilir).

## Açık kalan (tasks/impl)

- Callback endpoint rate-limit (S1 borcu) — ayrı; 087 eklemez, not düşer (reissue ucundaki mevcut TODO ile hizalı).