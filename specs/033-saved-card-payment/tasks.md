# Tasks: Kayıtlı Kartla Ödeme (NonSecure, Taksitli)

**Input**: Design documents from `/specs/033-saved-card-payment/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R9), data-model.md, contracts/payment-api.md,
quickstart.md

**Tests**: Payment aggregate saf testleri; iyzico charge/installment quickstart canlı (sandbox).

**Organization**: US1 (çekim) + US2 (taksit sorgu) gateway P1; US3 ECommerce uçtan uca P2.
Scope plumbing (payment.charge) Foundational — çekim uçları buna bağlı.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Taban çizgisi: `dotnet build` 0 hata + `dotnet test tests/Payment.Api.Tests` (032, 8
      test) yeşil.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: payment.charge scope zinciri + Payment aggregate/event — iki gateway story de buna bağlı.

- [X] T002 [P] `payment.charge` scope: `AuthorizationScopes.PaymentCharge = "payment.charge"` —
      `src/others/Common/Utils/Constants/AuthorizationScopes.cs`
- [X] T003 [P] Identity scope kaydı: `Config.ScopeResources["payment.charge"]="payment.api"` (AllApiScopes
      otomatik) — `src/others/Identity.Server/Config.cs`
- [X] T004 Identity Active demeti: `MerchantClientEventHandler.AddMerchantPermissions` Active koşuluna
      `payment.charge` ekle (cards.write yanı; Provisioning ALMAZ — fail-closed) —
      `src/others/Identity.Server/EventHandlers/MerchantClientEventHandler.cs`
- [X] T005 [P] `PaymentChargedEvent` record: `(Guid PaymentId, Guid MerchantId, decimal Price,
      decimal PaidPrice, int Installment, string IyzicoCommission, string IyzicoFee, string
      ProviderPaymentId)` — `src/others/Shared/IntegrationEvents.cs`
- [X] T006 [P] `PaymentStatus` enum (`Success=1, Failed=2`) —
      `src/services/Payment.Api/Domains/Payments/PaymentStatus.cs`
- [X] T007 `Payment` aggregate: `Succeeded(...)` + `Failed(...)` fabrikaları (data-model imzaları;
      zorunlu alan kontrolü; ResultDomain; `<remarks>Handler:</remarks>`); iyzico tipi GÖRMEZ —
      `src/services/Payment.Api/Domains/Payments/Payment.cs`
- [X] T008 [P] Payment aggregate testleri: Succeeded (alanlar+Success), Failed (Failed+ProviderPaymentId
      boş), boş merchantId/vaultToken reddi — `tests/Payment.Api.Tests/PaymentTests.cs`
- [X] T009 Payment.Api auth + event wire: `AddAuthenticationAndAuthorizationExtension(..., PaymentCharge)`;
      `PublishMessage<PaymentChargedEvent>` (mevcut PaymentCompleted exchange veya yeni fanout) —
      `src/services/Payment.Api/Program.cs`
- [X] T010 Checkpoint: `dotnet build` 0 hata + `dotnet test tests/Payment.Api.Tests` yeşil.

---

## Phase 3: User Story 2 — Taksit Seçenekleri Sorgusu (P1)

**Goal**: `POST /payments/installment-options` → iyzico taksit tablosu. (US1'den önce — çekimin
PaidPrice'ı bundan gelir.)

**Independent Test**: quickstart S1 — BIN+tutar → taksit seçenekleri.

- [X] T011 [US2] `InstallmentOptions` query slice: body `{Bin, Price}` → `InstallmentInfo.Retrieve`
      → taksit tablosu map (installmentNumber + totalPrice); endpoint `POST /installment-options`
      (`PaymentCharge`+`MerchantScoped`) —
      `src/services/Payment.Api/Domains/Payments/Features/Queries/InstallmentOptions.cs`
- [X] T012 [US2] Checkpoint: build 0 hata; quickstart S1 canlı (merchant token payment.charge scope'lu;
      BIN 552879 + 100 TL → taksit seçenekleri).

---

## Phase 4: User Story 1 — Gateway Çekim (P1) 🎯 MVP

**Goal**: `POST /payments` → saved-card NonSecure çekim → Payment kaydı + event.

**Independent Test**: quickstart S2 — vault token'la çekim; DB Payment + PAN sızma 0 + 1 event.

- [X] T013 [US1] `ChargePayment` command slice `[Transactional]`: body `{VaultToken, Price, PaidPrice,
      Installment, Buyer{...}, BasketItems[...]}`; StoredCard yükle (MerchantId eşleşme + Active →
      Revoked/yabancı reddi); `CreatePaymentRequest{PaymentCard{CardToken,CardUserKey}, Price,
      PaidPrice, Installment, Buyer, Shipping/BillingAddress(Buyer'dan), BasketItems, Currency=TRY,
      PaymentChannel/Group}` → `Payment.Create` (ProviderOptions inject); başarı (Status=="success")
      → `Payment.Succeeded` + Store + `PaymentChargedEvent` publish; başarısız → `Payment.Failed` +
      Store (olay YOK) + hata (INVALID_OPERATION_ERROR) —
      `src/services/Payment.Api/Domains/Payments/Features/Commands/ChargePayment.cs`
- [X] T014 [US1] `PaymentEndpointExtension` + Program.cs grup map (`api/v{version}/merchants/
      {merchantId:guid}/payments` — charge + installment-options) —
      `src/services/Payment.Api/Domains/Payments/PaymentEndpointExtension.cs`,
      `src/services/Payment.Api/Program.cs`
- [X] T015 [US1] Checkpoint: build 0 hata; quickstart S2 canlı (S0 kart sakla → çekim Success;
      DB Payment{ProviderPaymentId, IyzicoCommission/Fee, Success}; PAN/CVC sızma 0; 1 event;
      negatifler: Revoked kart 400, yabancı merchant 403, scope'suz token 403, taksitli Success).

---

## Phase 5: User Story 3 — ECommerce Checkout Uçtan Uca (P2)

**Goal**: ECommerce checkout → gateway charge; sipariş "ödendi". (ECommerce repo — implement'te keşif.)

**Independent Test**: quickstart S3 — ECommerce'ten kayıtlı kart + taksit → öde → sipariş ödendi.

- [ ] T016 [US3] ECommerce `GatewayPaymentClient` (charge + installment-options çağrıları;
      `GatewayCardTokenizer` deseni) + `MerchantTokenProvider` scope'una `payment.charge` ekle —
      `/Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/customer/Customer.Api/...` (keşif)
- [ ] T017 [US3] ECommerce checkout: kayıtlı kart seçimi (mevcut) + taksit seçenekleri sorgusu + seçim
      → gateway charge; Order.Api dönen PaymentId ile "ödendi" (stub yerine gerçek çağrı) — ECommerce
      WebApp/Order akışı (implement'te keşif)
- [ ] T018 [US3] ECommerce build 0 hata; quickstart S3 canlı (tarayıcı: kart+taksit → öde → sipariş
      ödendi + gateway Payment kaydı).

---

## Phase 6: Polish & Kapanış

- [ ] T019 Regresyon: `dotnet build` (çözüm) 0 hata; Payment + Merchant (47) + Commission (31) yeşil;
      ECommerce build 0 hata. Commit/PR kullanıcı onayıyla.

---

## Dependencies

```
T001 ─► Phase 2 (T002∥T003∥T005∥T006 → T004 → T007 → T008 → T009 → T010)
Phase 2 ─► US2 (T011 → T012)          # taksit sorgu; çekimin PaidPrice kaynağı
Phase 2 + US2 ─► US1 (T013 → T014 → T015)
US1+US2 ─► US3 (T016 → T017 → T018)   # ECommerce gateway sözleşmesini tüketir
hepsi ─► T019
```

## Parallel Opportunities

- T002 ∥ T003 ∥ T005 ∥ T006 (ayrı dosyalar); T008 aggregate testi T007 ile hizalı.

## Implementation Strategy

**MVP**: T001-T015 (Foundational + US2 + US1) — gateway kayıtlı kartla taksitli çekim yapar,
Payment kaydı + event üretir (curl-kanıtlı). US3 ECommerce'i bağlar (P2, ayrı repo). Scope plumbing
(T002-T004) Identity restart ister — Active merchant payment.charge'ı yenilenen token'da alır.
iyzico çağrısı gerçek (sandbox); charge/installment canlı checkpoint'lerde doğrulanır.

> Canlı S1+S2 GEÇTİ (2026-08-14): taksit sorgusu (BIN 552879 → tek çekim); çekim SUCCESS (iyzico providerPaymentId 37296146, gateway paymentId; DB Payment{iyzico maliyeti 3.49+0.25, Success}; PAN/CVC sızma 0; event yayınlandı/outbox temiz). Negatifler: bilinmeyen token→RECORD_NOT_FOUND, scope'suz token→403, yabancı merchant→403. CANLI FIX (3. sürpriz): payment.charge yeni scope — mevcut merchant OpenIddict client'ında yoktu; statü Passive→Active tetiğiyle MerchantClientEventHandler client'ı yeni demetle (payment.charge dahil) re-provision etti. US3 (ECommerce checkout) kullanıcıda.
