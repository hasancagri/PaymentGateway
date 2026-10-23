---
description: "Task list — 091 Store Lifecycle / Activation"
---

# Tasks: Store Lifecycle / Activation

**Input**: Design documents from `/specs/091-merchant-lifecycle-activation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Domain-TDD (anayasa İlke VIII) — saf domain (aggregate davranışı, VO, reconcile kararı)
test-first; handler/endpoint/adapter/hosted-form/webhook test-sonra/canlı (test task'ı YOK, canlı
quickstart doğrular).

**Organization**: User story bazlı. MVP = US1 + US2 (ikisi de P1 — aktivasyon döngüsü Live'a kadar).

## Path Conventions

Tek Payment BC: `src/services/Payment.Api/`, testler `tests/Payment.Api.Tests/`. Auth paylaşımı
`src/others/Common/` (mevcut, yeni dosya yok).

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 [P] `StoreOnboardingOptions` POCO (Ttl, FormBaseUrl, form metinleri, `BankCodes[]`) `src/services/Payment.Api/Options/StoreOnboardingOptions.cs`
- [X] T002 [P] `OnboardingStoreCallbackOptions` POCO (CallbackUrl non-secret + CallbackSecret SIR) `src/services/Payment.Api/Options/OnboardingStoreCallbackOptions.cs`
- [X] T003 [P] `CredentialEncryptionOptions` POCO (Key SIR — at-rest AES DEK) `src/services/Payment.Api/Options/CredentialEncryptionOptions.cs`
- [X] T004 `InterVposBankProfile`'a `InboundCallbackSecret` (SIR) alanı ekle (banka→PG webhook imza doğrulama) `src/services/Payment.Api/Options/InterVposBankOptions.cs`
- [X] T005 [P] `StoreOnboardingResourceConstants` (ONBOARDING_NOT_FOUND / LINK_EXPIRED / INVALID_PROFILE / BANK_FIELD_MISSING / ALREADY_SUBMITTED / DUPLICATE_ONBOARDING / CALLBACK_REJECTED / STORE_NOT_LIVE) `src/services/Payment.Api/Constants/StoreOnboardingResourceConstants.cs`
- [X] T006 [P] appsettings.json: `StoreOnboardingOptions` + `OnboardingStoreCallbackOptions` (non-secret) section + `BankCodes` `src/services/Payment.Api/appsettings.json`
- [X] T007 GlobalUsings: yeni namespace'ler (StoreOnboarding + ValueObjects + BankOnboarding + Credentials + Features) `src/services/Payment.Api/GlobalUsings.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Aggregate iskeleti + VO + port + credential-store + DI — tüm story'ler buna dayanır.

- [X] T008 [P] `StoreProfileTests` (test-first): zorunlu alan boş → INVALID_PROFILE; IBAN mod-97 geçersiz → red; MCC 4-hane dışı → red `tests/Payment.Api.Tests/StoreProfileTests.cs`
- [X] T009 `StoreProfile` VO (record + private ctor + static Create; IBAN mod-97) `src/services/Payment.Api/Domains/StoreOnboarding/ValueObjects/StoreProfile.cs`
- [X] T010 [P] `BankMerchantCredential` VO (transient sır; maskeli ToString; aggregate'e girmez) `src/services/Payment.Api/Domains/StoreOnboarding/ValueObjects/BankMerchantCredential.cs`
- [X] T011 `IBankOnboardingAdapter` port + `OnboardingReferral`/`OnboardingOutcome` (contracts/onboarding-bank-port.md) `src/services/Payment.Api/Domains/StoreOnboarding/BankOnboarding/IBankOnboardingAdapter.cs`
- [X] T012 `BankApplication` child entity (BankCode/Status/ReasonCode?/CredentialRef?/BankApplicationRef?/UpdatedAt) `src/services/Payment.Api/Domains/StoreOnboarding/BankApplication.cs`
- [X] T013 `StoreOnboarding` aggregate iskeleti + enum'lar (OnboardingStatus/BankApplicationStatus) + `Create` + private `_applications` `src/services/Payment.Api/Domains/StoreOnboarding/StoreOnboarding.cs`
- [X] T014 `IBankCredentialStore` + `EncryptedBankCredentialStore` (AES-GCM, DEK CredentialEncryptionOptions; şifreli Marten doküman; dönüşte sır yok) `src/services/Payment.Api/Domains/StoreOnboarding/Credentials/`
- [X] T015 Program.cs: 3 Options (T001-T003) BindConfiguration+Validate+singleton-bridge + credential-store DI + Marten index: OpenToken unique, CallbackToken unique, **MerchantId partial-unique where Status in (Draft/Submitted/Live)** (aynı store için tek aktif onboarding — FR-014 yarış kapısı, PaymentLink active-uniq deseni aynası) `src/services/Payment.Api/Program.cs`

---

## Phase 3: User Story 1 — Store başvurusunu başlat + profili topla + fan-out (P1) 🎯 MVP

**Goal**: super-admin talimatı → onboarding kaydı + TTL link → tek sabit hosted form → submit →
yapılandırılmış her bankaya referral fan-out (Referred / submit-öncesi Rejected).

**Independent Test**: talimat → link → form doldur → gönder → her banka `Referred` (eksik alan bankası `Rejected`), store `Submitted`.

- [X] T016 [US1] `StoreOnboarding.SubmitProfile` test (test-first): fan-out → Referred/Rejected entry; Draft değil → ALREADY_SUBMITTED; expired → LINK_EXPIRED `tests/Payment.Api.Tests/StoreOnboardingTests.cs`
- [X] T017 [US1] `StoreOnboarding.SubmitProfile` impl (profil set + per-banka BankApplication + Status=Submitted) `src/services/Payment.Api/Domains/StoreOnboarding/StoreOnboarding.cs`
- [X] T018 [US1] `SimulatorBankOnboardingAdapter` (alan doğrula → Submitted ack; banka-özel eksik → Rejected(BANK_FIELD_MISSING); private nested wire + normalize stub) `src/services/Payment.Api/Domains/StoreOnboarding/BankOnboarding/SimulatorBankOnboardingAdapter.cs`
- [X] T019 [US1] `CreateStoreOnboarding` command+handler ([Transactional]; Draft + tahmin-edilemez OpenToken/CallbackToken + TTL; DUPLICATE_ONBOARDING guard; opak handle döner) `src/services/Payment.Api/Domains/StoreOnboarding/Features/Commands/CreateStoreOnboarding.cs`
- [X] T020 [US1] `OpenOnboardingForm` command+handler (anonim, fail-closed: bilinmeyen/expired/kullanılmış → NotFound) + `StoreOnboardingPages.BuildOnboardingFormPage` (tek sabit form) `src/services/Payment.Api/Domains/StoreOnboarding/Features/Commands/OpenOnboardingForm.cs` + `StoreOnboardingPages.cs`
- [X] T021 [US1] `SubmitOnboardingProfile` command+handler ([Transactional]; StoreProfile.Create → her banka adapter.SubmitReferralAsync → SubmitProfile) `src/services/Payment.Api/Domains/StoreOnboarding/Features/Commands/SubmitOnboardingProfile.cs`
- [X] T022 [US1] `QueryOnboardingStatus` query+handler (opak handle + durum + per-banka status/reason; credential/sır YOK) `src/services/Payment.Api/Domains/StoreOnboarding/Features/Queries/QueryOnboardingStatus.cs`
- [X] T023 [US1] `StoreOnboardingEndpointExtension`: POST /store-onboarding [store-api-key], GET /{openToken} [anon], POST /{openToken}/submit [anon], GET /{pgStoreRef}/status [store-api-key] `src/services/Payment.Api/Domains/StoreOnboarding/StoreOnboardingEndpointExtension.cs`
- [X] T024 [US1] Program.cs: onboarding adapter fan-out (BankCodes/InterVposBankProfile → banka başına IBankOnboardingAdapter) + endpoint map + Discovery.IncludeType (yeni *Handler'lar) `src/services/Payment.Api/Program.cs`

**Checkpoint**: Başvuru başlatılır, form doldurulur, bankalara fan-out edilir; durum sorgulanır.

---

## Phase 4: User Story 2 — İlk banka onayında store canlı + unlock (P1) 🎯 MVP

**Goal**: banka→PG imzalı webhook (approve+credential) → credential güvenli saklanır → ilk onayda
store Live → store'a "Live" webhook → CreatePaymentLink artık Live şartı arar.

**Independent Test**: imzalı onay callback → store Live, credential at-rest şifreli, store webhook POST (credential'sız); Live öncesi CreatePaymentLink → STORE_NOT_LIVE, sonrası → link.

- [X] T025 [US2] `StoreOnboarding.RecordBankOutcome` test (test-first): ilk Approved→Live + credentialRef; 2. banka Approved→Live kalır; aynı banka tekrar Approved→no-op `tests/Payment.Api.Tests/StoreOnboardingTests.cs`
- [X] T026 [US2] `StoreOnboarding.RecordBankOutcome` impl (Approved→app+ilk-onayda Live; idempotent) `src/services/Payment.Api/Domains/StoreOnboarding/StoreOnboarding.cs`
- [X] T027 [US2] `SimulatorBankOnboardingAdapter.NormalizeCallback` (webhook ham gövde → OnboardingOutcome; Approved'da credential çıkarır) `src/services/Payment.Api/Domains/StoreOnboarding/BankOnboarding/SimulatorBankOnboardingAdapter.cs`
- [X] T028 [US2] `ReceiveBankOnboardingOutcome` command+handler ([Transactional]; CallbackToken eşleşme + HMAC doğrula → CALLBACK_REJECTED; normalize → credential-store.Save → RecordBankOutcome; ilk onayda OnboardingResultDelivery publish) `src/services/Payment.Api/Domains/StoreOnboarding/Features/Commands/ReceiveBankOnboardingOutcome.cs`
- [X] T029 [US2] `OnboardingResultDelivery` (Deliver record + tekil DeliverHandler; HMAC-SHA256 ham gövde, X-Signature, POST→non-2xx throw→durable retry; credential YOK) `src/services/Payment.Api/Domains/StoreOnboarding/Features/Commands/OnboardingResultDelivery.cs`
- [X] T030 [US2] Endpoint: POST /internal/onboarding/callback/{callbackToken} [anon + token + HMAC] + Program.cs Discovery.IncludeType(ReceiveBankOnboardingOutcome/OnboardingResultDelivery) `src/services/Payment.Api/Domains/StoreOnboarding/StoreOnboardingEndpointExtension.cs` + `Program.cs`
- [X] T031 [US2] `CreatePaymentLink` handler'a Live-guard: MerchantId için StoreOnboarding.Status==Live yoksa STORE_NOT_LIVE + guard testi `src/services/Payment.Api/Domains/PaymentLinks/Features/Commands/CreatePaymentLink.cs` + `tests/Payment.Api.Tests/PaymentLinkTests.cs`

**Checkpoint**: MVP tam — store ilk onayla Live, charge kapısı açılır, bildirim gider.

---

## Phase 5: User Story 3 — Banka reddi + yeniden başvuru (P2)

**Goal**: red bildirimi → o banka Rejected+reason (izole); super-admin reddeden bankaya yeniden başvurur.

**Independent Test**: red callback → o banka Rejected, store Live (≥1 onay varsa) + diğerleri değişmez; resubmit → o banka tekrar Referred.

- [X] T032 [US3] `StoreOnboarding` red-izolasyon + `Resubmit` test (test-first): Rejected bir bankayı ötekiler/Live etkilenmeden; Resubmit→Referred `tests/Payment.Api.Tests/StoreOnboardingTests.cs`
- [X] T033 [US3] `StoreOnboarding.Resubmit` impl (Rejected app → Referred, yeni referral) `src/services/Payment.Api/Domains/StoreOnboarding/StoreOnboarding.cs`
- [X] T034 [US3] `ResubmitBankApplication` command+handler ([Transactional]; adapter.SubmitReferralAsync tekrar) + endpoint POST /store-onboarding/{pgStoreRef}/banks/{bankCode}/resubmit [store-api-key] + Discovery.IncludeType `src/services/Payment.Api/Domains/StoreOnboarding/Features/Commands/ResubmitBankApplication.cs` + endpoint/Program

**Checkpoint**: Red süreci kilitlemez; reddeden bankaya yeniden başvuru çalışır.

---

## Phase 6: User Story 4 — Kaçan sonuçların mutabakatı (P3)

**Goal**: kaçan/geç webhook'u dayanıklı watchdog yakalar; "sürüyor"da erken terminal yok.

**Independent Test**: onay webhook'u hiç gelmemişken mutabakat tick → banka durumuyla hizalanır (onaylıysa Live); sürüyor → değişmez.

- [X] T035 [US4] `OnboardingReconciliation` test (test-first): bekleyen Approved→RecordBankOutcome; hâlâ sürüyor→değişmez (erken terminal yok) `tests/Payment.Api.Tests/OnboardingReconciliationTests.cs`
- [X] T036 [US4] `Process/OnboardingReconciliation` (Check + CheckHandler; Referred app'ler için adapter.QueryApplicationStatusAsync; ScheduleAsync tick; InDoubtReconciliation aynası) + Program.cs Discovery.IncludeType `src/services/Payment.Api/Domains/StoreOnboarding/Process/OnboardingReconciliation.cs` + `Program.cs`

**Checkpoint**: Kayıp bildirim store'u sonsuz beklemede bırakmaz.

---

## Phase 7: Polish & Cross-Cutting

- [X] T037 [P] FLOW.md: onboarding süreci adımları + Live-gate notu + yeni edge-anchor tipler `src/services/Payment.Api/FLOW.md`
- [X] T038 `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` yeşil (yeni anchor tipleri kodda var; BC haritası spec yolu) — gerekirse CLAUDE.md BC tablosuna StoreOnboarding origin spec satırı
- [X] T039 `dotnet build` (0 hata) + `dotnet test` (yeni testler + 089/090 bozulmadan) yeşil
- [ ] T040 Canlı Aspire E2E (quickstart S1-S8; banka onayı webhook'u elle/test-hook simüle) — TÜM geliştirme bitince topluca
- [ ] T041 Commit + PR (İlke V anayasa v3.0.0 amendment dahil); D9 charge-seam ve İlke-V EC-tarafı forward-requirement PR açıklamasında işaretli

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2)** → hepsi buna dayanır.
- **US1 (P3)** Foundational'a dayanır; MVP'nin ilk yarısı.
- **US2 (P4)** US1'e dayanır (aggregate + fan-out + adapter); MVP'nin ikinci yarısı → **MVP = US1+US2**.
- **US3 (P5)** US2'ye dayanır (RecordBankOutcome + callback + adapter referral).
- **US4 (P6)** US2'ye dayanır (RecordBankOutcome + adapter QueryApplicationStatus).
- **Polish (P7)** en son.

**Program.cs (T015/T024/T030/T034/T036) SIRALI** — aynı dosya, [P] DEĞİL.

## Parallel Opportunities

- Setup: T001/T002/T003/T005/T006 [P] (farklı dosyalar). T004/T007 ayrı.
- Foundational: T008(+T009), T010, T011, T012 [P] başlar; T013 T012'ye, T015 T001-T003+T014'e bağlı.
- US1 test-first: T016 → T017; T018/T019/T020 büyük ölçüde [P] (farklı dosya) ama T021 T017+T018'e, T024 Program sıralı.
- US2: T025→T026; T027/T029 [P]; T028 T026+T027+T014'e; T031 ayrı dosya [P].

## Implementation Strategy

**MVP (US1+US2):** aktivasyon döngüsü — başlat → form → fan-out → ilk onay → Live → charge unlock.
Bağımsız teslim edilebilir; US3/US4 dayanıklılık+red ekler. **D9 charge-seam** (per-store credential'ın
charge'da fiilen kullanılması) MVP DIŞI — BankSimulator onboarding + gerçek 2. store gelince bağlanır.
