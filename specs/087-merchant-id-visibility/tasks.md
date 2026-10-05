---
description: "Task list — MerchantId Görünürlük Politikası (PG bacağı)"
---

# Tasks: MerchantId Görünürlük Politikası (PG bacağı)

**Input**: `specs/087-merchant-id-visibility/` tasarım belgeleri
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/
**Tests**: Yalnız saf domain test-first (İlke VI) — `RegisterRequest.Submit`. Handler/endpoint/callback/teardown = test-sonra/canlı (conventions).
**Organization**: İki temel direk = US1 (HTML ekran sır göstermez) + US2 (MCP'de hassas veri yok). Yollar `src/services/Merchant.Api/` kökünden.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

---

## Phase 1: Setup

- [X] T001 [P] `OnboardingCallbackOptions` POCO ekle: `BootstrapRegistrationKey` + `CallbackSecret`; `AddOptions<T>().BindConfiguration(nameof(OnboardingCallbackOptions)).ValidateDataAnnotations().ValidateOnStart()` — `Options/OnboardingCallbackOptions.cs`, `Program.cs`
- [X] T002 [P] Dev secret'ları set: `dotnet user-secrets set OnboardingCallbackOptions:BootstrapRegistrationKey/CallbackSecret --project src/services/Merchant.Api` (section = `nameof(OnboardingCallbackOptions)`, T001 ile birebir); prod=vault notu (dev config, kod değil)

---

## Phase 2: Foundational (US1'i bloke eder — Domain-TDD, İlke VI)

**⚠️ CRITICAL**: T003/T004 tamamlanmadan US1 register/onay/callback yolu başlayamaz.

- [X] T003 [P] Domain test: `RegisterRequest.Submit` EXTEND — geçerli alanlarda Pending + `CorrelationId`+`CallbackUrl` set; boş/empty-Guid correlationId veya boş/geçersiz callbackUrl → `Error`; mevcut alan doğrulamaları korunur — `tests/Merchant.Api.Tests/RegisterRequestTests.cs`
- [X] T004 `RegisterRequest` EXTEND: `CorrelationId` (Guid) + `CallbackUrl` (string) private-set alanları + `Submit` imzasına iki parametre (T003 yeşil edene dek) — `Domains/RegisterRequests/RegisterRequest.cs`

**Checkpoint**: Başvuru aggregate'i correlation+callbackUrl taşıyor; US1 akışı başlayabilir.

---

## Phase 3: User Story 1 — Merchant sürecinde HTML ekran sır göstermez (Priority: P1) 🎯 MVP

**Goal**: Credential insana hiç render edilmez; makine-handoff (HMAC-callback) ile store'a gider; reveal/form/resend sökülür; reissue ekransız.

**Independent Test**: reveal/form/resend route 404; onay credential döndürmez ama store callbackUrl'ine imzalı credential POST edilir; reissue yeni key'i callback'le teslim eder.

### Teardown (direk A)

- [X] T005 [P] [US1] TEARDOWN `Domains/CredentialRevealLinks/` tümü (`CredentialRevealLinkEndpointExtension` `GET /onboarding/reveal/{token}` + `CredentialRevealLink` aggregate + `RevealCredentials`) + `Program.cs` map çağrısı — `Domains/CredentialRevealLinks/`, `Program.cs`
- [X] T006 [P] [US1] TEARDOWN `Domains/OnboardingFormSessions/` tümü (endpoint `GET/POST /onboarding/{token}` + aggregate + `CreateFormSession` + `Pages/` form HTML + `.csproj` embedded-resource) + `Program.cs` map — `Domains/OnboardingFormSessions/`, `Pages/`, `Merchant.Api.csproj`, `Program.cs`
- [X] T007 [P] [US1] TEARDOWN `AdminResendCredentialLink` command + MCP tool + `resend_credential_link` sabitleri (`Shared/McpToolNames.cs` + `Shared/McpToolDescriptions.cs`) + `MerchantAdminSurface.ToolScopeMap` girdisi — `Domains/RegisterRequests/Features/Agents/Commands/AdminResendCredentialLink.cs`, `src/others/Shared/*`, `Mcp/MerchantAdminSurface.cs`

### Callback gönderici (041 aynası)

- [X] T008 [US1] `DeliverCredentialCallback` ekle: `Deliver(CallbackUrl, CorrelationId, MerchantId, MerchantKey, Status)` record + `DeliverHandler.Handle` (ham JSON → `X-Signature`=HMAC-SHA256(`CallbackSecret`,raw-body) hex → POST → `EnsureSuccessStatusCode` durable retry); MerchantKey loglanmaz; ad TEKİL `Handler` değilse `Program.cs` `IncludeType` — `Domains/Merchants/Features/Commands/DeliverCredentialCallback.cs`, `Program.cs` (kontrat: `contracts/credential-callback.md`)

### Register yolu (store→PG)

- [X] T009 [US1] `SubmitRegistration` yazma slice + endpoint: `POST /api/v1/onboarding/register` (m2m `merchant.write` + `X-Registration-Key` constant-time doğrulama vs `BootstrapRegistrationKey`, geçersiz→401/403); `RegisterRequest.Submit(... correlationId, callbackUrl)` persist Pending; aynı correlationId/mükerrer-email→`409` idempotent; `202 {accepted,correlationId,status:"Pending"}`; `400` doğrulama — `Domains/RegisterRequests/Features/Commands/SubmitRegistration.cs`, `Program.cs` (depends T004; kontrat: `contracts/register-endpoint.md`)

### Onay dönüşümü

- [X] T010 [US1] `AdminApproveRegistration` EXTEND: reveal-link oluşturma + `SendEmailRequested` bloğunu TÜMDEN kaldır (mail'le hiç link gitmez — karar 2026-10-05); `request.CallbackUrl`+`CorrelationId` okuyup `DeliverCredentialCallback.Deliver(...,"Active")` publish; dönüş zaten MerchantKey-free (korunur; MerchantId tutamaç kalır) — `Domains/RegisterRequests/Features/Agents/Commands/AdminApproveRegistration.cs` (depends T008)

### Reissue dönüşümü

- [X] T011 [US1] `ReissueMerchantKey` EXTEND: `ReissueRequest`'e `correlationId`+`callbackUrl`; `RevealUrl` dönüşü + reveal-link öldürme bloğunu kaldır; `DeliverCredentialCallback` publish (echo correlation); dönüş kabul-makbuzu (key/RevealUrl-free); `MerchantKeyReissued` event + salt-append log korunur — `Domains/Merchants/Features/Commands/ReissueMerchantKey.cs` (depends T008)

### Wiring + süreç belgesi

- [X] T012 [US1] Mevcut `Onboarding` options reveal alanlarını (`RevealLinkLifetime`/`PublicBaseUrl`) temizle (reveal söküldü, başka tüketici yoksa) + `Program.cs` ilgili kayıt — `Options/Onboarding.cs`, `Program.cs`
- [X] T013 [US1] `FLOW.md` güncelle (onboarding süreci: form→register, reveal→makine-callback, resend söküm); `scripts/check-flow-links.sh` yeşil — `src/services/Merchant.Api/FLOW.md`

**Checkpoint**: US1 bağımsız çalışır — register→onay→callback + reissue→callback; hiçbir ekran/dönüş credential göstermez.

---

## Phase 4: User Story 2 — MCP üzerinden hassas veri gelmez (Priority: P1)

**Goal**: Tool/query dönüşleri credential + finansal/PII-free; MerchantScoped GetMerchant key scrub.

**Independent Test**: tüm MCP tool + MerchantScoped query dönüşleri MerchantKey/iban/taxNumber/identityNumber içermiyor; nöbetçi grep temiz.

- [X] T014 [US2] MerchantScoped `GetMerchant` dönüşünden `MerchantKey` çıkar (scrub, FR-B2); dönüş DTO'su sır-free — `Domains/Merchants/Features/Queries/GetMerchant.cs`
- [X] T015 [P] [US2] Nöbetçi denetim: `MerchantKey`/`iban`/`taxNumber`/`identityNumber` hiçbir `ILogger`/tool-dönüşü/query DTO'sunda değil (admin query tool'ları 044'ten zaten temiz — teyit); sızma varsa düzelt — repo geneli

**Checkpoint**: US2 bağımsız doğrulanır — LLM/agent yüzeyi yalnız sır-olmayan veri görür.

---

## Phase 5: Polish & Cross-Cutting

- [X] T016 [P] FR-C1: ele-alma politikası (render YOK / kayıt S2S / dönüş HMAC-callback / kullanım gRPC / LLM opak tutamaç) + ADR ref `adr-mcp-control-plane-no-secret-return` — `CLAUDE.md`, `docs/conventions.md`
- [X] T017 `dotnet build` + `dotnet test tests/Merchant.Api.Tests` yeşil
- [X] T018 quickstart Senaryo 1-8 canlı doğrulandı (Aspire + ECommerceAgent store). S1/S3/S4/S5/S6 canlı GEÇTİ; S2 fail-closed (401) + S7 scrub (kod+nöbetçi) + S8 red-callback-yok kod-teyit. Karar 5 reissue cross-repo simetrisi: store tarafı 3 fix gerektirdi (full-KYC register gövdesi, callback validator DI, reissue callback YARIŞ fix'i — pending PG'den önce commit + StartRegistration overwrite-safe). PG değişmedi.

---

## Dependencies & Execution Order

- **Setup (T001-T002)** → Foundational.
- **Foundational (T003-T004; test-first: T003 ÖNCE, T004 sonra)** → US1 register/onay/callback yolunu bloke eder.
- **US1**: Teardown (T005-T007) US1 davranışından bağımsız, paralel. Callback T008 → onay T010 + reissue T011 (ikisi T008'e bağlı). Register T009 T004'e bağlı. T012-T013 süreç netleştikten sonra.
- **US2 (T014-T015)**: US1'den BAĞIMSIZ — Foundational gerektirmez, paralel başlayabilir (scrub + nöbetçi register yoluna değmez).
- **Polish (T016-T018)**: US1+US2 sonrası. T018 store E2E bekler.

## Parallel Opportunities

- Setup: T001, T002 paralel.
- Teardown: T005, T006, T007 paralel (ayrı dosyalar/klasörler).
- US2 (T014-T015) US1 ile tümüyle paralel.
- T015, T016 paralel.

## Implementation Strategy

### MVP (US1)

1. Setup (T001-T002) → Foundational (T003-T004).
2. US1 (T005-T013): teardown + callback + register + onay/reissue dönüşümü + FLOW.
3. **DUR + DOĞRULA**: direk A — hiçbir ekran/dönüş credential göstermiyor; callback çalışıyor.

### İkinci dilim (US2)

4. US2 (T014-T015) — paralel koşulabilir; direk B nöbetçisi.

### Kapanış

5. Polish (T016-T018); T018 store ile uçtan-uca.

## Notes

- Söküm ağırlıklı US1 — ölü kod + sır-ekrana-girer yüzeyi giderir; `dotnet build` her teardown sonrası kırık referansı yakalar.
- Callback 041 `StoreCallbackDelivery` byte-yakın ayna — yeni desen icat etme.
- Reissue cross-repo simetrisi (Karar 5) canlı E2E'den önce store tarafında teyit edilir.
- İki sır (`BootstrapRegistrationKey`/`CallbackSecret`) + mevcut m2m client creds; MerchantKey asla log/trace/dönüş.