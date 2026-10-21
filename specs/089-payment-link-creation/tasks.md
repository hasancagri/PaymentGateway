---
description: "Task list — PaymentLink Üretimi (Pay-by-Link ilk dilim)"
---

# Tasks: PaymentLink Üretimi (Pay-by-Link ilk dilim)

**Input**: Design documents from `/specs/089-payment-link-creation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/pg-payment-link.md

**Tests**: Domain-TDD (anayasa İlke VIII/VI) — **saf domain (aggregate davranışı) test-first**. Handler/endpoint/HTML test-SONRA (host harness yok; quickstart ile canlı doğrulama).

**Organization**: Görevler user story'ye göre gruplanır. 088 `HostedPayments/` slice'ı bu dilimde `PaymentLinks/`'e evrilir (research D5).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, bekleyen bağımlılık yok)
- **[Story]**: US1/US2/US3 (spec.md)
- Her görevde tam dosya yolu

## Path

Tek BC: `src/services/Payment.Api/` + `tests/Payment.Api.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Slice evrimi + kapsam-dışı seed teardown + sabitler/options.

- [X] T001 `git mv src/services/Payment.Api/Domains/HostedPayments` → `Domains/PaymentLinks`; namespace `Payment.Api.Domains.HostedPayments` → `Payment.Api.Domains.PaymentLinks` (tüm dosyalar).
- [X] T002 Kapsam-dışı seed slice'larını SİL: `src/services/Payment.Api/Domains/PaymentLinks/Features/Commands/CompleteHostedPayment.cs` + `StoreCallbackDelivery.cs` (sağlayıcı-callback + HMAC store-bildirim = sonraki dilim; dev teardown, migration yok).
- [X] T003 [P] `src/services/Payment.Api/Constants/HostedPaymentResourceConstants.cs` → `PaymentLinkResourceConstants.cs`: EKLE `LINK_NOT_FOUND`/`LINK_EXPIRED`/`LINK_CANCELLED`; KORU `UNSUPPORTED_CURRENCY`/`INVALID_REQUEST`/`PROVIDER_UNAVAILABLE`/`TERMINAL_VIOLATION`; SİL `MERCHANT_NOT_ACTIVE`/`INITIALIZE_FAILED`/`SESSION_NOT_FOUND`.
- [X] T004 [P] `src/services/Payment.Api/Options/HostedPaymentOptions.cs` → `PaymentLinkOptions.cs`: alanlar `Ttl`(00:30:00)/`GraceWindow`(00:05:00)/`LinkBaseUrl`/`TrustPageTitle`/`TrustPageIntro`; SİL `CallbackSecret`/`ReturnPageBaseUrl`/`ReturnSuccessText`/`ReturnFailureText`; `Program.cs` binding adını güncelle + `appsettings.json` değerleri (non-secret).

**Checkpoint**: Slice adı + sabitler + options PaymentLink'e göre; derleme henüz kırık (aggregate T005'te).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tüm story'lerin paylaştığı aggregate iskeleti + index + test harness.

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz.

- [X] T005 `src/services/Payment.Api/Domains/PaymentLinks/HostedPaymentSession.cs` → `PaymentLink.cs`: `PaymentLink : AggregateRoot` alanlar (`MerchantId`,`OrderRef`,`Amount`,`Currency`,`ReturnUrl`,`StoreName`,`OrderSummary`,`ExpiresAt`,`OpenToken`,`Status`) + enum `PaymentLinkStatus {Pending,Opened,Paid,Expired,Cancelled}` + `IsTerminal` getter; sağlayıcı alanlarını (`CallbackToken`/`CheckoutFormToken`/`HostedUrl`/`ProviderPaymentId`/`FailureReason`) + eski `Start`/`AttachCheckoutForm`/`MarkSucceeded`/`MarkFailed` metotlarını DÜŞÜR (davranış story fazlarında eklenir). Parametresiz private ctor korunur (Marten).
- [X] T006 `src/services/Payment.Api/Program.cs` Marten index: seed mutlak `(MerchantId,OrderRef)` unique → **partial unique** (yalnız aktif `{Pending=0,Opened=1}`, `IndexDefinition.Predicate`) + `OpenToken` unique (research D1). **DOĞRULA (F4):** Newtonsoft `Status` enum'u JSON'a int mi string mi yazıyor — predicate (`data->>'Status' in ('0','1')` vs `in ('Pending','Opened')`) buna göre; canlı DB'de üretilen index'i teyit et (yanlış biçim aktif-tekil + yarış korumasını SESSİZCE kırar).
- [X] T007 [P] `tests/Payment.Api.Tests/HostedPaymentSessionTests.cs` → `PaymentLinkTests.cs`: xUnit iskelet + `Pending()` factory helper (yeni `Create` imzası); eski test gövdeleri story fazlarında yazılır.

**Checkpoint**: Aggregate kabuğu + index + test dosyası hazır; davranış yok.

---

## Phase 3: User Story 1 — Store ödeme linki üretir (Priority: P1) 🎯 MVP

**Goal**: Yetkili store geçerli istekle Pending, TTL'li, (store,orderRef) aktif-tekil bir link + opak referans alır (idempotent).

**Independent Test**: `POST /payment-links` geçerli girdiyle `linkUrl`+`pgPaymentRef` döner; tekrar çağrı aynı linki döner; TRY-dışı/tutar≤0 reddedilir (quickstart S1).

### Tests for User Story 1 (Domain-TDD — ÖNCE yaz, FAIL doğrula) ⚠️

- [X] T008 [P] [US1] `tests/Payment.Api.Tests/PaymentLinkTests.cs`: `Create` testleri — TRY kabul/USD red, amount≤0 red, boş orderRef/returnUrl/storeName red, geçerli→Pending + `ExpiresAt` set.
- [X] T009 [P] [US1] `tests/Payment.Api.Tests/PaymentLinkTests.cs`: `Expire` testleri — aktif + `now>ExpiresAt` → Expired (lazy); terminal → no-op.

### Implementation for User Story 1

- [X] T010 [US1] `src/services/Payment.Api/Domains/PaymentLinks/PaymentLink.cs`: `static ResultDomain<PaymentLink> Create(...)` (TRY/amount/alan doğrulama → Pending) + `ResultDomain Expire(DateTime now)` (lazy terminal). (T005,T008,T009)
- [X] T011 [US1] `src/services/Payment.Api/Domains/PaymentLinks/Features/Commands/CreatePaymentLink.cs`: `record Command`(MerchantId,Amount,Currency,OrderRef,ReturnUrl,StoreName,OrderSummary) + `Response`(LinkUrl,PgPaymentRef) + `[Transactional] Handler` — alan doğrula, aktif `(MerchantId,OrderRef)` yükle → varsa+geçerli idempotent-dön / varsa+past-expiry `Expire`+Store sonra devam / yoksa devam; `OpenToken` üret + `ExpiresAt=now+Ttl` + `Create` + Store; partial-unique yarış çakışmasını yakala→var olanı dön (research D1 Create akışı).
- [X] T012 [US1] `src/services/Payment.Api/Domains/PaymentLinks/PaymentLinkEndpointExtension.cs`: `AddHostedPaymentEndpointExtension`→`AddPaymentLinkEndpointExtension`; `POST /payment-links` ucu (`CreatePaymentLinkBody` record, `payment.write`, `InvokeAsync`→CreatePaymentLink) + `Program.cs` map adını güncelle.

**Checkpoint**: US1 bağımsız çalışır — link üretimi + idempotent (MVP).

---

## Phase 4: User Story 2 — Müşteri linki açar, güven bağlamını görür (Priority: P1)

**Goal**: Müşteri gizli token'lı linki açar; kart formundan ÖNCE güven bağlamı (mağaza+sipariş+tutar+son geçerlilik) görür + link Opened'a geçer; "Öde" stub döner.

**Independent Test**: Geçerli açılış token'lı GET güven bağlamı render eder + Opened; sahte token→404; "Öde"→PROVIDER_UNAVAILABLE, link terminale geçmez (quickstart S2).

**Depends**: US1 (Create + Expire gerekir).

### Tests for User Story 2 (Domain-TDD — ÖNCE yaz, FAIL doğrula) ⚠️

- [X] T013 [P] [US2] `tests/Payment.Api.Tests/PaymentLinkTests.cs`: `Open` testleri — Pending→Opened, zaten Opened idempotent Ok, terminal red, `now>ExpiresAt`→Expired+`LINK_EXPIRED`.
- [X] T014 [P] [US2] `tests/Payment.Api.Tests/PaymentLinkTests.cs`: `IsPayable` testleri — Opened + `now<=ExpiresAt+grace`→Ok; past-grace→Expire+`LINK_EXPIRED`; Opened-değil→red.

### Implementation for User Story 2

- [X] T015 [US2] `src/services/Payment.Api/Domains/PaymentLinks/PaymentLink.cs`: `ResultDomain Open(DateTime now)` (lazy expiry + Pending→Opened, idempotent) + `ResultDomain IsPayable(DateTime now, TimeSpan grace)` (saf guard). (T010)
- [X] T016 [US2] `src/services/Payment.Api/Domains/PaymentLinks/Features/Commands/OpenPaymentLink.cs`: `record Command`(OpenToken) + `Response`(StoreName,OrderSummary,Amount,ExpiresAt) + `[Transactional] Handler` — OpenToken lookup (bulunamaz→`LINK_NOT_FOUND`), `Open(now)`, güven bağlamı verisi dön.
- [X] T017 [US2] `src/services/Payment.Api/Domains/PaymentLinks/PaymentLinkEndpointExtension.cs`: `GET /payment-links/{openToken}` (`AllowAnonymous`, OpenPaymentLink çağırır, başarı→güven bağlamı HTML, bulunamaz/expired→404) + `BuildTrustContextPage(...)` HTML (seed `BuildReturnPage` evrim: mağaza/sipariş/tutar/son geçerlilik + "Öde" formu, kart alanı YOK).
- [X] T018 [US2] `src/services/Payment.Api/Domains/PaymentLinks/Features/Commands/PayPaymentLink.cs`: `record Command`(OpenToken) + `Handler` — OpenToken lookup, `IsPayable(now,grace)` guard, geçerliyse `PROVIDER_UNAVAILABLE` (durum DEĞİŞMEZ) + `POST /payment-links/{openToken}/pay` ucu (`AllowAnonymous`) EndpointExtension'a.

**Checkpoint**: US1+US2 bağımsız çalışır — açılış + güven bağlamı + stub ödeme.

---

## Phase 5: User Story 3 — Link ölümü: süre dolumu ve iptal (Priority: P2)

**Goal**: Store linki iptal eder (Cancelled); süresi dolan link Expired; her ikisinde açma/ödeme reddedilir; terminal sonrası aynı orderRef ile re-create serbest.

**Independent Test**: İptal edilen link açılamaz (`LINK_CANCELLED`); TTL geçmiş link açma→Expired; terminal link'e işlem→`TERMINAL_VIOLATION`; terminal sonrası re-create yeni link (quickstart S3).

**Depends**: US1 (Create gerekir). Expired lazy geçişi US1 `Expire` + US2 `Open`'da zaten canlı.

### Tests for User Story 3 (Domain-TDD — ÖNCE yaz, FAIL doğrula) ⚠️

- [X] T019 [P] [US3] `tests/Payment.Api.Tests/PaymentLinkTests.cs`: `Cancel` testleri — Pending→Cancelled, Opened→Cancelled, terminal→`TERMINAL_VIOLATION`.

### Implementation for User Story 3

- [X] T020 [US3] `src/services/Payment.Api/Domains/PaymentLinks/PaymentLink.cs`: `ResultDomain Cancel()` (aktif→Cancelled, terminal→`TERMINAL_VIOLATION`). (T005,T019)
- [X] T021 [US3] `src/services/Payment.Api/Domains/PaymentLinks/Features/Commands/CancelPaymentLink.cs`: `record Command`(PgPaymentRef) + `[Transactional] Handler` — `LoadAsync<PaymentLink>(id)` (bulunamaz→`LINK_NOT_FOUND`), `Cancel()`, Store + `POST /payment-links/{pgPaymentRef}/cancel` ucu (`payment.write`) EndpointExtension'a.

**Checkpoint**: Üç story de bağımsız işlevsel; yaşam döngüsü kapalı.

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Süreç belgesi + keşif + canlı doğrulama + guard'lar.

- [X] T022 [P] `src/services/Payment.Api/FLOW.md` güncelle (İlke IX, aynı PR): yeni Süreç adımları (Create→Pending / Open→Opened güven bağlamı / Pay stub / Cancel / lazy Expire); sağlayıcı-callback + HMAC store-bildirim + dönüş-sayfası adımlarını KALDIR; kenar-anchor tip adları `PaymentLink.*`.
- [X] T023 Wolverine keşif doğrula: yeni handler'lar `*Handler` son-ekli → `Program.cs` `IncludeAssembly` kapsar; `IncludeType` gerekmez (teyit; gerekirse ekle).
- [X] T024 `dotnet build` (0 hata) + `dotnet test` (PaymentLinkTests yeşil).
- [X] T025 Guard'lar: `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` yeşil.
- [ ] T026 `specs/089-payment-link-creation/quickstart.md` canlı doğrulama (Aspire): S1 (üretim+idempotent SC-001/002), S2 (güven bağlamı+fail-closed+stub SC-004), S3 (iptal+lazy expire+terminal re-create SC-003), PAN sızıntı denetimi (SC-005).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001-T004)**: hemen başlar.
- **Foundational (T005-T007)**: Setup sonrası; TÜM story'leri bloklar.
- **US1 (T008-T012)**: Foundational sonrası — MVP.
- **US2 (T013-T018)**: US1 sonrası (Create+Expire bağımlı).
- **US3 (T019-T021)**: US1 sonrası (Create bağımlı); US2'den bağımsız.
- **Polish (T022-T026)**: istenen story'ler bitince.

### Within Each Story

- Domain testleri (T008/T009, T013/T014, T019) implementasyondan ÖNCE, FAIL doğrulanır.
- Aggregate davranışı → command slice → endpoint sırası.
- `PaymentLink.cs` + `PaymentLinkEndpointExtension.cs` + `PaymentLinkTests.cs` story'ler arası PAYLAŞILIR → aynı dosya görevleri [P] DEĞİL (sıralı).

### Parallel Opportunities

- Setup: T003, T004 [P] (farklı dosya).
- Her story domain testleri [P] (ör. T008+T009, T013+T014) — ama hepsi `PaymentLinkTests.cs` TEK dosya → pratikte ardışık yaz; mantıksal olarak bağımsız.
- US2 ve US3 (US1 sonrası) ayrı geliştiriciyle paralel yürütülebilir; `PaymentLink.cs` çakışmasına dikkat (US2=Open/IsPayable, US3=Cancel farklı metot bloğu).

---

## Parallel Example: Setup

```bash
Task: "T003 Constants → PaymentLinkResourceConstants.cs"
Task: "T004 Options → PaymentLinkOptions.cs + Program binding + appsettings"
```

---

## Implementation Strategy

### MVP First (US1)

1. Setup (T001-T004) → 2. Foundational (T005-T007) → 3. US1 (T008-T012) → **DUR + DOĞRULA** (quickstart S1) → demo.

### Incremental

US1 (link üretimi) → US2 (açılış + güven bağlamı + stub) → US3 (iptal + süre dolumu). Her story bağımsız test + commit. Polish sonda (FLOW + canlı + guard).

---

## Notes

- [P] = farklı dosya, bağımlılık yok.
- Domain testleri FAIL doğrulanmadan impl yazılmaz (İlke VIII).
- Handler'da kullanıcıdan gelmeyen HİÇBİR literal yok → `PaymentLinkOptions` (CLAUDE.md).
- Banka/sağlayıcı/çekim + store webhook + dönüş sayfası KAPSAM DIŞI (sonraki dilim).
- Dev aşaması: defansif migration yok, şema yeniden kurulur.
