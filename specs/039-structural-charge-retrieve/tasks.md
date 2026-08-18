---
description: "Task list — 039 Structural Charge + Retrieve"
---

# Tasks: Yapısal İdempotent Çekim + Retrieve Yüzeyi

**Input**: `/specs/039-structural-charge-retrieve/` (spec.md, plan.md)

**Tests**: İlke VI (Domain-TDD) — Payment aggregate (Begin/Succeed/Fail) test-first; test task'ı
implementasyondan ÖNCE. Handler/auth/endpoint: test-sonra / canlı doğrulama.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Foundational (Blocking Prerequisites)

### Aggregate (DOMAIN, test-first)

- [X] T001 [P] TEST `tests/Payment.Api.Tests/PaymentTests.cs` — `Begin(...)` → Charging + correlationKey;
  `Succeed(...)` Charging→Success + providerPaymentId; `Fail()` Charging→Failed; boş key/provider reddi
- [X] T002 `Domains/Payments/Payment.cs` — +`CorrelationKey`, +`PaymentStatus.Charging=3`, +`Begin`
  factory, +`Succeed`/`Fail` mutators (ResultDomain); eski Succeeded/Failed KALIR; T001'i geçir
- [X] T003 `Program.cs` — Marten `Schema.For<Payment>().Duplicate(x=>x.CorrelationKey, unique + partial
  predicate 'correlation_key is not null')` + MerchantId index (retrieve filtresi)

### API-key auth (Model 2)

- [X] T004 `Domains/MerchantStatus/MerchantApiKeyReference.cs` — YENİ doc `{Guid Id; string KeyHash}`
- [X] T005 `Domains/MerchantStatus/MerchantLifecycleEventHandler.cs` — Created/Provisioned'da
  SHA-256(MerchantKey) → MerchantApiKeyReference upsert (StatusChanged dokunmaz)
- [X] T006 `Program.cs` — Marten `Schema.For<MerchantApiKeyReference>().Index(x=>x.KeyHash, unique)`
- [X] T007 `Auth/ApiKeyAuthenticationHandler.cs` — YENİ AuthenticationHandler "ApiKey": X-Api-Key oku →
  SHA-256 → IQuerySession lookup → bulundu ise `merchant_id` claim principal; yok/boş → Fail
- [X] T008 `Program.cs` — `.AddAuthentication().AddScheme<...>("ApiKey", null)` + `AddAuthorizationBuilder()
  .AddPolicy("MerchantApiKey", p => { p.AuthenticationSchemes.Add("ApiKey"); MerchantScopeRequirement })`

---

## Phase 2: US1 — İdempotent yapısal çekim (P1) 🎯 MVP

- [X] T009 [US1] `Features/Commands/ChargePayment.cs` repurpose — body: +correlationKey, −basketItems;
  handler: Active gate + vault → StoredCard + buyer VO + sentetik kalem (agent-path deseni)
- [X] T010 [US1] ChargePayment idempotency: load-by-correlationKey → Success/Failed var-olanı dön;
  Charging → pending dön; yoksa `Begin` marker Store + SaveChanges (provider ÖNCESİ, FR-012)
- [X] T011 [US1] ChargePayment iyzico çekim → `payment.Succeed(...)` / `payment.Fail()`; unique-violation
  yakala → reload → var-olanı dön (yarış FR-003); PaymentChargedEvent success'te
- [X] T012 [US1] ChargePaymentResponse status **lowercase** map (success/failed/pending) + paymentId=
  Payment.Id + price/paidPrice + correlationKey echo; endpoint `.RequireAuthorization("MerchantApiKey")`
- [ ] T013 [US1] Canlı doğrulama — ECom S1 (re-onboarding sonrası key hash var; charge → Confirmed)

---

## Phase 3: US2 — Retrieve (verify + reconcile) (P1)

- [X] T014 [US2] `Features/Queries/RetrievePayment.cs` — by-key: `GET /` `?correlationKey=`; by-id:
  `GET /{paymentId:guid}`; MerchantId (route) filtresi = kiracı sınırı (FR-010)
- [X] T015 [US2] RetrievePayment map: Charging→pending, Success→success, Failed→failed; bulunamadı →
  `Results.NotFound()` (404, FR-007); yanıt charge ile aynı alanlar
- [X] T016 [US2] `PaymentEndpointExtension.cs` — RetrievePayment endpoint'lerini gruba ekle; "MerchantApiKey"
- [ ] T017 [US2] Canlı doğrulama — ECom S2 (retrieve-by-key verify) + bilinmeyen key → 404

---

## Phase 4: Polish

- [X] T018 [P] Tüm domain testleri yeşil (`dotnet test tests/Payment.Api.Tests`); `dotnet build` temiz
- [ ] T019 Canlı S3/S4 (çift tetik → tek çekim; yanıt kaybı → retrieve kurtarma) ECom ile uçtan uca
- [ ] T020 README/docs — feature kapanınca (039 yapısal çekim yüzeyi)

---

## Dependencies

- Phase 1 (Foundational) tüm story'leri bloklar. T001→T002 (test-first).
- US1 (Phase 2) = MVP. US2 (Phase 3) US1 üstüne.
- Canlı doğrulama (T013/T017/T019): mevcut merchant **re-onboarding** ister (key hash yakalansın).
