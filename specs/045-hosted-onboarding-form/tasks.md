# Tasks: Hosted Onboarding Form + Tek Kullanımlık Credential Teslimi

**Input**: specs/045-hosted-onboarding-form/ (spec, plan, research, data-model, contracts, quickstart)

**Tests**: saf domain (iki yeni aggregate) test-first ZORUNLU; endpoint/HTML/mail canlı doğrulama.

**NOT**: Store tarafı (ECom 078) hazır (commit 290d181); store-E2E doğrulamaları iki sistem
birlikte koşarak yapılır. US4 sökümü ancak P1-P4 canlı PASS sonrası (FR-009).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 `Options/Onboarding.cs`: ölü `ActivationBaseUrl` → `PublicBaseUrl` (Required) +
  `FormLinkLifetime` (varsayılan 24 saat) + `RevealLinkLifetime` (varsayılan 1 saat);
  appsettings/dev config güncelle (`PublicBaseUrl: http://localhost:5202`); mevcut binding kalır.

## Phase 2: User Story 1 — Form oturumu + hosted başvuru formu (P1) 🎯

- [X] T002 [US1] Domain testleri ÖNCE (red): `OnboardingFormSession.Create/Consume/IsUsable`
  (mutlu yol, süre, çift tüketim, token URL-safe/uzunluk/teklik) —
  `tests/Merchant.Api.Tests/OnboardingFormSessionTests.cs`
- [X] T003 [US1] `OnboardingFormSession` aggregate (green; data-model şeması) —
  `src/services/Merchant.Api/Domains/OnboardingFormSessions/OnboardingFormSession.cs`;
  Marten index `Token` + `Email` (Program.cs)
- [X] T004 [US1] `CreateFormSession` command: e-posta doğrula, yaşayan Pending `RegisterRequest`
  varsa `formUrl:null + applicationStatus:"Pending"`, yoksa oturum üret + `{formUrl, expiresAt,
  applicationStatus}` — `Features/Commands/CreateFormSession.cs`; endpoint
  `POST /api/v1/onboarding/sessions` (merchant.write) — `OnboardingFormSessionEndpointExtension.cs`
- [X] T005 [US1] Hosted form sayfaları: `GET /onboarding/form/{token}` (029 alan seti, type'a
  koşullu alanlar; usable-değilse nötr sayfa) + `POST` → `SubmitOnboardingForm` command
  (`RegisterRequest.Submit`; hata=form yeniden, oturum YAŞAR; başarı=oturum tüket + PG Admin
  bildirim maili + teşekkür sayfası) — aynı extension + `Features/Commands/SubmitOnboardingForm.cs`
- [ ] T006 [US1] Canlı doğrulama quickstart P1 (curl + tarayıcı + Mailpit).

## Phase 3: User Story 2 — Approve → mail + tek gösterim (P1)

- [X] T007 [P] [US2] Domain testleri ÖNCE (red): `CredentialRevealLink.Create/Consume/IsUsable/Kill`
  — `tests/Merchant.Api.Tests/CredentialRevealLinkTests.cs`
- [X] T008 [US2] `CredentialRevealLink` aggregate (green) —
  `src/services/Merchant.Api/Domains/CredentialRevealLinks/CredentialRevealLink.cs`;
  Marten index `Token` + `MerchantId` (Program.cs)
- [X] T009 [US2] Approve kancası: `AdminApproveRegistration` handler'ına reveal link üretimi +
  `SendEmailRequested` (IsHtml, teslim linki + yönerge; outbox aynı transaction) —
  `Domains/RegisterRequests/Features/Agents/Commands/AdminApproveRegistration.cs`
- [X] T010 [US2] Teslim sayfası: `GET /onboarding/reveal/{token}` — usable ise MerchantId +
  MerchantKey BİR KEZ göster + `Consume` (GET tüketir — gösterim=teslim); değilse "PG Admin'e
  başvurun" nötr sayfası — `CredentialRevealLinkEndpointExtension.cs`
- [X] T011 [US2] `admin_resend_credential_link` MCP tool (merchant.admin): Approved merchant'ın
  yaşayan linklerini `Kill`, yeni link + mail; yanıtta key/link YOK; `Shared.MerchantAdminTools`'a
  sabit — `Features/Agents/Commands/AdminResendCredentialLink.cs` + `Shared/McpToolNames.cs`
- [ ] T012 [US2] Canlı doğrulama quickstart P2 (approve → Mailpit → ilk/ikinci açılış → resend).

## Phase 4: User Story 3 — S2S durum + validate (P1)

- [X] T013 [P] [US3] `GetOnboardingApplicationStatus` query + `GET /api/v1/onboarding/
  applications/{email}` (merchant.read): en güncel başvuru; yoksa `status:"None"` (200); yanıtta
  kimlik/sır alanı YOK — `Domains/RegisterRequests/Features/Queries/GetOnboardingApplicationStatus.cs`
- [X] T014 [P] [US3] `ValidateMerchantCredentials` query + `POST /api/v1/onboarding/credentials/
  validate` (merchant.read): eşleşme + Active → `{valid}`; ikili loglanmaz —
  `Domains/Merchants/Features/Queries/ValidateMerchantCredentials.cs`
- [X] T015 [US3] Canlı doğrulama quickstart P3 + store-E2E P4 (078 S1-S3+S5, iki sistem birlikte).

## Phase 5: User Story 4 — Eski MCP yüzeyi sökümü (P2; P1-P4 CANLI PASS SONRASI)

- [X] T016 [US4] `SubmitRegistration.cs` + `RegistrationStatus.cs` sil (literal tool adları —
  Shared sabiti yok); admin bildirim mailinin form POST'unda yaşadığını doğrula; build + test.
- [X] T017 [US4] Canlı doğrulama quickstart P5. — KAPANIŞ: kullanıcı kararıyla canlı tur atlandı; söküm + build/test/guard kanıt sayıldı (2026-09-19).

## Phase 6: Polish & Cross-Cutting

- [X] T018 [P] Merchant `FLOW.md` güncelle (İLKE VII, aynı PR): başvuru doğuşu hosted form; teslim
  mail + tek gösterimlik sayfa; eski MCP teslim adımı sil — `scripts/check-flow-links.sh` yeşil.
- [X] T019 [P] `CLAUDE.md` Merchant BC satırı güncelle (hosted onboarding + teslim; 029 MCP teslim
  yolu emekli).
- [X] T020 Tam doğrulama: `dotnet build` + `dotnet test` + guard script'leri.

## Dependencies & Execution Order

- T001 → US1/US2 (Options'ı ikisi kullanır). US1 (T002→T005) ∥ US2 (T007→T011) — farklı dosyalar;
  T009 approve kancası T008'i bekler. US3 (T013/T014 [P]) bağımsız, T001 sonrası her an.
- T016-T017 ANCAK T006+T012+T015 canlı PASS sonrası (FR-009). Polish en son.
