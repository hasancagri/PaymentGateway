---
description: "Task list — 041 Hosted-CF Ödeme Yüzeyi"
---

# Tasks: Hosted-CF Ödeme Yüzeyi (iyzico Checkout Form)

**Input**: `/specs/041-hosted-cf-payment/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Tests**: Yalnız saf domain birim testi (İlke VI, Domain-TDD — aggregate test-first). Handler/endpoint/wire
canlı doğrulanır (quickstart, sandbox). Ek entegrasyon testi ÜRETİLMEZ.

**Organization**: User story bazlı; her story bağımsız test edilebilir artım.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

- **[P]**: paralel çalışabilir (farklı dosya, bağımlılık yok)
- Yollar `src/services/Payment.Api` ve `tests/Payment.Api.Tests` köküne göre.

---

## Phase 1: Setup (Paylaşılan altyapı)

**Purpose**: Config + sabit + Options iskeleti (kod öncesi).

- [X] T001 [P] `src/services/Payment.Api/Constants/HostedPaymentResourceConstants.cs` — hata kodları (Payment BC sahipli): `HOSTED_PAYMENT_MERCHANT_NOT_ACTIVE`, `HOSTED_PAYMENT_UNSUPPORTED_CURRENCY`, `HOSTED_PAYMENT_INVALID_REQUEST`, `HOSTED_PAYMENT_PROVIDER_UNAVAILABLE`, `HOSTED_PAYMENT_INITIALIZE_FAILED`, `HOSTED_PAYMENT_SESSION_NOT_FOUND`, `HOSTED_PAYMENT_TERMINAL_VIOLATION`.
- [X] T002 [P] `src/services/Payment.Api/Options/HostedPaymentOptions.cs` — Options POCO (DataAnnotations `[Required]`): `CallbackSecret`, `IyzicoCallbackBaseUrl` (C1: `/internal/payments/callback` mutlak taban; runtime `+ "/" + CallbackToken`), `ReturnPageBaseUrl`, `ReturnSuccessText`, `ReturnFailureText`. XML doc: handler'da literal yasak → tüm non-user değer buradan (D8/D9).
- [X] T003 `src/services/Payment.Api/appsettings.json` — `HostedPaymentOptions` bölümü (secret olmayan: `IyzicoCallbackUrl`, `ReturnPageBaseUrl`, metinler). `CallbackSecret` user-secrets'a (git'e girmez). `IyzicoRequestOptions` CF path'leri mevcut — repurpose (değişmez).

---

## Phase 2: Foundational (Bloklayan önkoşullar)

**Purpose**: Aggregate + auth policy + Marten şema — tüm story'lerin temeli.

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz.

- [X] T004 [P] `tests/Payment.Api.Tests/HostedPaymentSessionTests.cs` — **test-first** (İlke VI): Start→Pending (CallbackToken set); AttachCheckoutForm (Pending iken bağlar, terminal iken RET); MarkSucceeded (Pending→Succeeded, ProviderPaymentId set); MarkFailed (Pending→Failed, reason set); terminal-idempotent (Succeeded'a MarkSucceeded no-op Ok); terminal-ihlali (Succeeded'a MarkFailed RET, Failed'a MarkSucceeded RET); Start guard (amount≤0 / boş orderRef / boş callbackUrl / boş callbackToken RET).
- [X] T005 `src/services/Payment.Api/Domains/HostedPayments/HostedPaymentSession.cs` — aggregate root (`AggregateRoot`): alanlar (data-model §1, **CallbackToken dahil**) + `HostedPaymentStatus` enum (aynı dosya) + `Start(...,callbackToken)`/`AttachCheckoutForm`/`MarkSucceeded`/`MarkFailed` (`ResultDomain`, terminal tek-yön guard) + `IsTerminal` getter. T004 yeşil eder.
- [X] T006 `src/services/Payment.Api/Program.cs` — Marten şema: `.Index(x => new { x.MerchantId, x.OrderRef }, i => i.IsUnique = true)` (FR-004) + `.Index(x => x.CallbackToken, i => i.IsUnique = true)` (C1 callback lookup + tekil sır).
- [X] T007 `src/services/Payment.Api/Program.cs` — `HostedPaymentApiKey` policy: ApiKey şeması + `RequireAuthenticatedUser()` (route'ta merchantId YOK; tenant claim'den — D1). Policy adı `AuthorizationPolicies` yerine yerel sabit (Payment-özel) veya Common'a ekleme — mevcut `MerchantApiKey`'i KULLANMA (route zorunlu → fail-closed).
- [X] T008 `src/services/Payment.Api/Options/HostedPaymentOptions.cs` bağlama — Program.cs: `AddOptions<HostedPaymentOptions>().BindConfiguration(nameof(HostedPaymentOptions)).ValidateDataAnnotations().ValidateOnStart()` + düz POCO singleton inject (IyzicoRequestOptions deseni).

**Checkpoint**: Aggregate + policy + şema hazır — story'ler başlayabilir.

---

## Phase 3: User Story 1 - Hosted ödeme linki üretimi (P1) 🎯 MVP

**Goal**: Store `POST /hosted-payment` (X-Api-Key) → iyzico CF initialize → `{HostedUrl, PgPaymentRef}`.

**Independent Test**: Active merchant + geçerli tutar/OrderRef/CallbackUrl → 200 çalışan HostedUrl + PgPaymentRef;
session Pending saklı. Tekrar OrderRef yeni kayıt açmaz. Pasif merchant / non-TRY reddedilir.

- [X] T009 [US1] `src/services/Payment.Api/Domains/HostedPayments/Features/Commands/InitiateHostedPayment.cs` — slice: command `{Amount, Currency, OrderRef, CallbackUrl}` (MerchantId claim'den) + `Response {HostedUrl, PgPaymentRef}` + **nested CF initialize wire** (`InitializeCheckoutFormRequest` / `CheckoutFormInitializeResult` — contracts/iyzico-cf-wire.md §1). Handler `[Transactional]`.
- [X] T010 [US1] `InitiateHostedPayment.cs` handler mantığı: (a) Currency != "TRY" → RET `UNSUPPORTED_CURRENCY`; Amount≤0 → RET `INVALID_REQUEST`. (b) `MerchantStatusReference` yükle (claim merchant_id) — yok/`Status!="Active"` → RET `MERCHANT_NOT_ACTIVE` (fail-closed, FR-002). (c) mevcut (MerchantId, OrderRef) Pending session varsa yeni AÇMA, mevcut HostedUrl+ref dön (FR-004). (d) `callbackToken = Guid.NewGuid("N")` üret → `HostedPaymentSession.Start(...,callbackToken)` → session.Store.
- [X] T011 [US1] `InitiateHostedPayment.cs` iyzico çağrısı: buyer/basket/adres **sentezle** (D4; sandbox nominal + tek kalem price=Amount), `callbackUrl = HostedPaymentOptions.IyzicoCallbackBaseUrl + "/" + session.CallbackToken` (C1), `conversationId = PgPaymentRef (session.Id "N")` (I1 kararı — OrderRef değil), path/locale/currency `IyzicoRequestOptions`'tan. `RestHttpClientV2.PostAsync` (try/catch → `PROVIDER_UNAVAILABLE`). Başarısız status/boş token → RET `INITIALIZE_FAILED`. Başarı → `AttachCheckoutForm(token)`, `HostedUrl=paymentPageUrl` dön.
- [X] T012 [US1] `src/services/Payment.Api/Domains/HostedPayments/HostedPaymentEndpointExtension.cs` — `POST /hosted-payment` map (`HostedPaymentApiKey` policy), `IMessageBus.InvokeAsync`, MerchantId claim'den command'a. Program.cs'te `app.Map...` çağrısı ekle.

**Checkpoint**: US1 bağımsız test edilebilir (quickstart Senaryo 1).

---

## Phase 4: User Story 2 - Ödeme sonucunun store'a bildirimi (P1)

**Goal**: iyzico dönüşü → CF retrieve doğrula → terminal → store'a imzalı bildirim + müşteri dönüş sayfası.

**Independent Test**: Pending session için iyzico dönüşü simüle → store CallbackUrl'ine imzalı sonuç POST'u
gider (başarı/başarısız) + müşteri dönüş sayfasını görür. Store erişilemezse sonuç kalıcı, yeniden gönderilir.

- [X] T013 [US2] `src/services/Payment.Api/Domains/HostedPayments/Features/Commands/CompleteHostedPayment.cs` — slice: command `{CallbackToken, IyzicoToken}` + **nested CF retrieve wire** (`RetrieveCheckoutFormRequest` / `RetrieveCheckoutFormResult` — contracts/iyzico-cf-wire.md §2). Handler `[Transactional]`.
- [X] T014 [US2] `CompleteHostedPayment.cs` handler: **CallbackToken ile session bul** (C1 secret-token kapısı; yoksa RET `SESSION_NOT_FOUND` = 404, store'a bildirim yok). Zaten terminal ise retrieve'i ATLA, mevcut sonuçla store-bildirim publish'i tekrarla (US3 idempotent). CF retrieve çağır iyzico `IyzicoToken` ile (try/catch → RET, session Pending kalır — yeniden denenebilir).
- [X] T015 [US2] `CompleteHostedPayment.cs` sonuç: `status==success && paymentStatus=="SUCCESS"` → `MarkSucceeded(paymentId)`; aksi → `MarkFailed(errorCode ?? paymentStatus)`. session.Update.
- [X] T016 [US2] `src/services/Payment.Api/Domains/HostedPayments/Features/StoreCallbackDelivery.cs` — durable local Wolverine mesajı `StoreCallbackDelivery.Deliver {PgPaymentRef, CallbackUrl, TxRef, Status, ReasonCode?}` + **tekil `...Handler`** (Wolverine keşif — çoğul yasak): ham JSON gövde üret → `X-Signature = HMAC-SHA256(HostedPaymentOptions.CallbackSecret, raw_body)` → store CallbackUrl'ine POST (FR-007). Hata fırlat → Wolverine durable retry (FR-009).
- [X] T017 [US2] `CompleteHostedPayment.cs` — terminal geçiş sonrası `[Transactional]` içinde `IMessageBus.PublishAsync(StoreCallbackDelivery.Deliver)` (outbox — DB commit'te gider, FR-009 kayıpsız).
- [X] T018 [US2] `src/services/Payment.Api/Domains/HostedPayments/HostedPaymentEndpointExtension.cs` — `POST /internal/payments/callback/{callbackToken}` map (C1 secret-token kapılı — D5/Complexity): route'tan `callbackToken` + form `token` oku → `CompleteHostedPayment` invoke (SESSION_NOT_FOUND → 404) → müşteri tarayıcısını `/payments/return/{pgPaymentRef}` 302 yönlendir.
- [X] T019 [US2] `HostedPaymentEndpointExtension.cs` — `GET /payments/return/{pgPaymentRef}` map: session yükle, `Status`'a göre `HostedPaymentOptions.ReturnSuccessText`/`ReturnFailureText` ile minimal HTML dön (FR-010). Program.cs map çağrıları ekle.

**Checkpoint**: US1+US2 = tam ödeme akışı (quickstart Senaryo 2).

---

## Phase 5: User Story 3 - Güvenlik + tekrar-dayanıklılık (P2)

**Goal**: İmza doğrulanabilir; aynı dönüş tekrar gelse sonuç tek; tekrar-bildirim aynı sonucu taşır.

**Independent Test**: (a) yanlış anahtarla imza → store reddeder. (b) Succeeded session'a tekrar callback →
durum değişmez, store'a aynı sonuç tekrar gider.

- [X] T020 [US3] `tests/Payment.Api.Tests/HostedPaymentSessionTests.cs` — idempotency/terminal senaryoları GENİŞLET (T004 üstüne): tekrar `MarkSucceeded` aynı ProviderPaymentId korur; Failed→Succeeded ve Succeeded→Failed RET (`TERMINAL_VIOLATION`). (Aggregate zaten guard'lı — testle kilitle.)
- [X] T021 [US3] `CompleteHostedPayment.cs` doğrula: terminal session'da retrieve atlanır + store-bildirim aynı Status/ReasonCode ile TEKRAR publish edilir (çift-callback → tek terminal, tekrar-bildirim aynı sonuç — FR-008/SC-003). T014 mantığını sertleştir + yorumla.
- [X] T022 [US3] İmza sözleşmesi doğrulaması (canlı): `StoreCallbackDelivery` gövdesi ham-body ile birebir imzalanır (serialize-sonra-imzala; store aynı ham body'yi doğrular). Kod incelemesi + quickstart Senaryo 3.1 (yanlış secret → red).

**Checkpoint**: Tüm story'ler tamam (quickstart Senaryo 3).

---

## Phase 6: Polish & Cross-Cutting

- [X] T023 [P] PAN/CVV sızıntı denetimi (SC-005): handler/log/wire'da kart alanı yok — iyzico yalnız token/paymentId döner; log'larda hassas alan loglanmadığını doğrula.
- [X] T024 [P] `CLAUDE.md` Payment BC satırı güncelle: hosted-CF ödeme yüzeyi CANLI (kart-vault söküktü → ödeme yönü eklendi); `/mcp` hâlâ tool'suz (store HTTP, agent değil).
- [X] T025 Build + test yeşil doğrula: `dotnet build` (0 hata) + `dotnet test` (Payment.Api.Tests HostedPaymentSession yeşil). Aspire'dan quickstart Senaryo 1-3 canlı sandbox.

---

## Bağımlılıklar / Sıra

- **Setup (T001-T003)** → **Foundational (T004-T008)** → story'ler.
- **US1 (T009-T012)**: Foundational'a bağlı; MVP. Tek başına teslim edilebilir.
- **US2 (T013-T019)**: Foundational + US1 aggregate/session'a bağlı (US1 olmadan test için session elle kurulabilir, ama akış US1'i ister).
- **US3 (T020-T022)**: US1+US2 üstüne sertleştirme (aggregate guard'ı foundational'da — US3 testle kilitler).
- **Polish (T023-T025)**: en son.

## Paralel fırsatlar

- Setup: T001 ∥ T002 (farklı dosya). T003 sonra.
- Foundational: T004 (test) T005 öncesi yazılır; T006/T007/T008 Program.cs — **aynı dosya, seri**.
- US1: T009 wire+shell; T010/T011 aynı dosya seri; T012 ayrı dosya sonra.
- US2: T013 ∥ T016 (farklı dosya: CompleteHostedPayment ∥ StoreCallbackDelivery). T014/T015/T017 CompleteHostedPayment seri; T018/T019 endpoint dosyası.
- Polish: T023 ∥ T024.

## MVP kapsamı

**US1 (Phase 1+2+3)** = MVP: ödeme başlatma + hosted URL. Ödeme akışının giriş kapısı. US2 kapanışı ekler,
US3 sertleştirir.

## Bağımsız test kriterleri

- **US1**: Active merchant → 200 HostedUrl+ref; tekrar-OrderRef tek kayıt; pasif/non-TRY red.
- **US2**: iyzico dönüşü → terminal + store imzalı bildirim + dönüş sayfası; store erişilemezse retry.
- **US3**: yanlış-imza store'da red; çift-callback tek terminal + aynı tekrar-bildirim.
