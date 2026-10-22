---
description: "Task list — NonSecure Banka Çekim Dilimi"
---

# Tasks: NonSecure Banka Çekim Dilimi (+ çok-banka akıllı routing)

**Input**: `/specs/090-nonsecure-bank-charge/` (plan.md, spec.md, research.md, data-model.md, contracts/)

**Tests**: Domain-TDD (anayasa İlke VIII) — saf domain (aggregate davranışı, router kararı, VO, BinCard lookup) test-first. Adapter/handler/endpoint/hosted sayfa test-sonra / canlı.

**Organization**: US bazlı. Öncelik: US1 (P1) → US2 (P2) → US4 (P2) → US3 (P3).

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

- **[P]**: Paralel (farklı dosya, bağımsız)
- Tüm yollar `src/services/Payment.Api/` (aksi belirtilmedikçe)

---

## Phase 1: Setup (Paylaşılan İskelet)

- [X] T001 [P] `Constants/PaymentLinkResourceConstants.cs`'e red taksonomi kodları ekle (`CHARGE_DECLINED`; `PROVIDER_UNAVAILABLE` tükenmiş technical için yeniden kullanılır)
- [X] T002 [P] `Options/BankRoutingOptions.cs` oluştur (OrderedBankCodes + health/circuit seam alanı — TODO yorumlu)
- [X] T003 [P] `Options/PosAccountOptions.cs` oluştur (acquirer başına BankCode + SupportedCardTypes + SupportsNonSecure + CostPriority; liste)
- [X] T004 [P] `Options/SimulatorBankOptions.cs` oluştur (BankCode + test-veri sentinel'leri: onay/yetersiz-bakiye/teknik/fraud)
- [X] T005 [P] `Options/StoreCallbackOptions.cs` oluştur (CallbackUrl + CallbackSecret; secret yorumuyla)
- [X] T006 `appsettings.json`'a `BankRoutingOptions` + `PosAccountOptions` (3 acquirer) + `SimulatorBankOptions` (3 banka) section'ları ekle; `CallbackSecret` user-secrets'a (appsettings'e DEĞİL — yorum düş)
- [X] T007 `Program.cs`'e yeni Options kayıtları (bind+ValidateDataAnnotations+ValidateOnStart + singleton-bridge; mevcut PaymentLinkOptions bloğunu aynala)

---

## Phase 2: Foundational (TÜM story'leri bloklar)

**⚠️ Bitmeden hiçbir US başlayamaz — domain çekirdeği + port + adapter + webhook.**

### Domain-TDD testleri (önce, FAIL etmeli)

- [X] T008 [P] `tests/Payment.Api.Tests/PaymentLinkTests.cs`'e test: `RecordAttempt(Approved)`→Paid+authCode; `RecordAttempt(Declined)`→Opened+append; terminal link→`TERMINAL_VIOLATION`; Paid sonrası ikinci attempt reddi (çift-çekim guard); `PaymentModel` default NonSecure
- [X] T009 [P] `tests/Payment.Api.Tests/PaymentLinkTests.cs`'e test: `BankChargeOutcome.Approved/Declined` factory alanları (IsApproved/DeclineClass/RawCode)

### Implementasyon

- [X] T010 [P] `Domains/PaymentLinks/PaymentLink.cs`'e `enum PaymentModel { NonSecure = 0 }` + `enum DeclineClass { IssuerDecline, TechnicalFailure, ConfigError, FraudDecline }` ekle
- [X] T011 [P] `Domains/PaymentLinks/ValueObjects/BankChargeOutcome.cs` oluştur (private ctor + `Approved`/`Declined` static factory; alanlar IsApproved/DeclineClass?/ProviderRef?/AuthCode?/RawCode)
- [X] T012 [P] `Domains/PaymentLinks/PaymentAttempt.cs` oluştur (child entity, AggregateRoot değil; BankCode/IsApproved/DeclineClass?/ProviderRef?/AuthCode?/RawCode/AttemptedAt; PAN YOK)
- [X] T013 `Domains/PaymentLinks/PaymentLink.cs`: `_attempts` koleksiyon + `IReadOnlyList Attempts` + `PaymentModel` alanı (Create'te NonSecure set) + `RecordAttempt(BankChargeOutcome, DateTime): ResultDomain` + mevcut `IsPayable` gövdesini private `PayableGuard` helper'a çıkar (bağımlı: T010-T012)
- [X] T014 [P] `Domains/PaymentLinks/BankCharge/IBankPaymentAdapter.cs` oluştur (port: `BankCode` + `ChargeAsync(BankChargeRequest, ct)`) + `BankChargeRequest`/`CardData` transient input VO (saklanmaz yorumu, İlke VI)
- [X] T015 `Domains/PaymentLinks/BankCharge/SimulatorBankAdapter.cs` oluştur (`ISingletonDependency`; private nested `SimulatorChargeRequestWire`/`SimulatorChargeResponseWire` + private `ResponseMapper.MapNonSecure`; test-veri sentinel'leriyle canned sonuç; `Map3DS` seam yorumlu) (bağımlı: T011, T014, T004)
- [X] T016 `Domains/PaymentLinks/BankCharge/BankChargeRouter.cs` oluştur (`IScopedDependency`; `IEnumerable<IBankPaymentAdapter>`; sıralı dene + DeclineClass failover kararı: Approved/Issuer/Fraud→dur, Technical/Config→sıradaki, tükeniş→son technical; **çift-çekim seam `<summary>`'de belgele**) (bağımlı: T011, T014)
- [X] T017 `Domains/PaymentLinks/Features/Commands/StoreResultDelivery.cs` oluştur (`Deliver` record + tekil `DeliverHandler`; HMAC-SHA256 ham JSON gövde + `X-Signature` lowercase hex + non-2xx throw→durable retry; `StoreCallbackOptions` secret) (041 `dccf97b` aynası)
- [X] T018 `Program.cs`: gerekirse `opts.Discovery.IncludeType(typeof(StoreResultDelivery.DeliverHandler))` (nested handler keşif güvencesi)

**Checkpoint**: Domain çekirdeği + bir adapter + router + webhook hazır; US'ler başlayabilir.

---

## Phase 3: US1 — Müşteri öder, çekim başarılı (P1) 🎯 MVP

**Goal**: Açık link + onay kartı → Paid + store imzalı bildirim + PaymentCompleted.

**Independent Test**: Onay dönen kartla `/pay` → link Paid, 1 başarılı attempt, store webhook + `PaymentCompletedEvent`.

- [X] T019 [US1] `Domains/PaymentLinks/Features/Commands/PayPaymentLink.cs` yeniden yaz: command `(OpenToken, CardData Card)`; `[Transactional]`; load→`IsPayable`→`BankChargeRequest` kur→`router` çağır→her outcome `RecordAttempt`→`session.Store`; onayda `PaymentCompletedEvent` + `StoreResultDelivery.Deliver` publish; **yalnız `InvokeAsync`** (PAN outbox'a girmesin) (bağımlı: T013, T015, T016, T017)
- [X] T020 [US1] `Domains/PaymentLinks/PaymentLinkEndpointExtension.cs`: güven bağlamının altına kart formu + `pay()` JS form alanlarını JSON gövde POST eder; `/pay` ucu `[FromBody]` kart gövdesi bağlar (AllowAnonymous kalır); kart gövdesi request-log'a girmesin
- [X] T021 [US1] Onay yanıtı: `redirectUrl=ReturnUrl` döndür; hosted sayfa başarı→yönlendirme (contracts/pay-endpoint.md)

**Checkpoint**: Mutlu yol uçtan uca (tek banka onay yeterli).

---

## Phase 4: US2 — Issuer reddi (P2)

**Goal**: Red kartı → link açık kalır, normalize red mesajı, PaymentFailed.

**Independent Test**: Yetersiz-bakiye sentinel kartı → Paid'e geçmez, 1 başarısız attempt (IssuerDecline), `CHARGE_DECLINED`, `PaymentFailedEvent`.

- [X] T022 [P] [US2] `tests/Payment.Api.Tests/BankChargeRouterTests.cs`'e test: Issuer/Fraud reddinde router DURUR (sıradaki banka çağrılmaz); fake adapter doubles ile
- [X] T023 [US2] `PayPaymentLink.cs`: declined dalı — `DeclineClass`→resource kod map (serbest metin yok); link Opened kalır; `PaymentFailedEvent` + `StoreResultDelivery.Deliver(failed)` publish (bağımlı: T019)
- [X] T024 [US2] `PaymentLinkEndpointExtension.cs`: decline render — "başka kartla deneyin" normalize mesaj (kart verisi/sır dönmez)

**Checkpoint**: Red akışı; link yeniden denenebilir.

---

## Phase 5: US4 — Akıllı yönlendirme: yetenek eleme + on-us (P2)

**Goal**: BIN→issuer çözümü; aday = yetenek-ele → on-us → maliyet sırası.

**Independent Test**: Kart BIN'i bir acquirer issuer'ına çözülürse o banka (on-us) önce; desteklemeyen banka elenir; bilinmeyen BIN→fail-open.

### Domain-TDD testleri (önce)

- [X] T025 [P] [US4] `tests/Payment.Api.Tests/BinCardTests.cs` oluştur: `ResolveByPan` prefix eşleşme + bilinmeyen BIN→null
- [X] T026 [P] [US4] `tests/Payment.Api.Tests/BankChargeRouterTests.cs`'e test: 2-aşama aday — yetenek eleme + on-us öncelik + maliyet sırası + bilinmeyen BIN fail-open (maliyet sırası)

### Implementasyon

- [X] T027 [P] [US4] `Domains/BinCards/BinCard.cs` oluştur (düz Marten doküman: Bin/IssuerBankCode/Scheme/CardType)
- [X] T028 [P] [US4] `Domains/BinCards/bin-seed.json` (küçük sim BIN seti, embedded resource) + `Domains/BinCards/BinCardSeeder.cs` (startup seed)
- [X] T029 [US4] BinCard lookup (`ResolveByPan(pan)`→`BinCard?`) — seeder/lookup servisi (bağımlı: T027, T028)
- [X] T030 [US4] `BankChargeRouter.cs` zenginleştir: BinCard resolve → `PosAccountOptions` yetenek eleme → on-us (issuer==BankCode) + CostPriority sıralaması → aday listesi; sonra Phase-2 failover döngüsü aday listesini izler (bağımlı: T016, T029, T003)
- [X] T031 [US4] `Program.cs`: BinCard startup seed çağrısı (`BinCardSeeder`)

**Checkpoint**: Routing aday seçimi akıllı; failover aday sırasını izler.

---

## Phase 6: US3 — Teknik hatada canlı failover, 3 banka (P3)

**Goal**: 3 simüle banka canlı; technical/config → sıradaki aday, onaya dek.

**Independent Test**: İlk aday technical, ikinci onay → ikinci onaylar; issuer/fraud→durur; ilk onay→kalan çağrılmaz; hepsi technical→`PROVIDER_UNAVAILABLE`.

- [X] T032 [P] [US3] `tests/Payment.Api.Tests/BankChargeRouterTests.cs`'e test: Technical→sıradaki; hepsi-technical→son technical; ilk-approved→kalan çağrılmaz; Config→failover (3 fake adapter)
- [X] T033 [US3] 3 `SimulatorBankAdapter` örneğini config'le kaydet (banka başına `SimulatorBankOptions`; named/parametrize DI) — `Program.cs`/DI (bağımlı: T015)
- [X] T034 [US3] `appsettings.json`: 3 banka + 3 PosAccount + sentinel verisi (ilk technical, ikinci onay senaryosu dahil)
- [X] T035 [US3] `BankChargeRouter.cs`: aday listesi üzerinde failover döngüsünü 3-banka ile doğrula/pekiştir (Phase-2 + Phase-5 mantığı; gerekirse düzelt) (bağımlı: T030, T033)

**Checkpoint**: Canlı çok-banka failover.

---

## Phase 7: Polish & Çapraz Kesit

- [X] T036 `src/services/Payment.Api/FLOW.md` güncelle: yeni adım (çekim/routing/failover/Paid/webhook) + kenar-anchor tipleri (`PaymentLink.RecordAttempt`, `BankChargeRouter`, `StoreResultDelivery.Deliver`, `SimulatorBankAdapter`, `BinCard`); çift-çekim hazard notu + boundary (durum-sorgu/3DS/2.-canlı-banka sonraki versiyon)
- [X] T037 [P] Guard'lar: `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` yeşil
- [X] T038 İlke VI denetimi: PAN/CVV + CallbackSecret hiçbir kalıcı ortamda (DB/log/outbox/yanıt/trace) görünmüyor — grep + inceleme
- [X] T039 `dotnet build` (0 hata) + `dotnet test` (089 + yeni testler yeşil)
- [ ] T040 Canlı Aspire E2E (quickstart.md; **tüm geliştirme bitince, topluca** — 089 T026+F4 index teyidi dahil)

---

## Dependencies & Execution Order

- **Setup (P1)** → bağımsız, önce.
- **Foundational (P2)** → Setup sonrası; TÜM US'leri bloklar.
- **US1 (P3 faz)** → Foundational sonrası (MVP).
- **US2** → Foundational sonrası; US1 handler'ını genişletir (T023 bağımlı T019).
- **US4** → Foundational sonrası; router'ı zenginleştirir (T030 bağımlı T016).
- **US3** → Foundational + US4 (aday listesi) sonrası; 3-banka + failover doğrulama.
- **Polish (P7)** → tüm US'ler sonrası.

### Kritik dosya çakışmaları (sıralı, [P] DEĞİL)
- `PaymentLink.cs`: T010 → T013
- `BankChargeRouter.cs`: T016 → T030 → T035
- `PayPaymentLink.cs`: T019 → T023
- `PaymentLinkEndpointExtension.cs`: T020 → T024
- `Program.cs`: T007 → T018 → T031 → T033
- `appsettings.json`: T006 → T034
- `BankChargeRouterTests.cs`: T022, T026, T032 aynı dosya → sıralı

### Paralel fırsatlar
- Setup T001-T005 [P]. Foundational testleri T008-T009 [P]; enum/VO/entity/port T010-T012, T014 [P]. US4 T025-T028 [P]. US3 T032 [P].

---

## Implementation Strategy

- **MVP = Setup + Foundational + US1.** Onaylı çekim → Paid → store bildirim. Burada durup doğrulanabilir.
- **Artımlı**: +US2 (red) → +US4 (akıllı routing) → +US3 (3-banka failover).
- Canlı doğrulama + commit/PR **en sona, topluca** (089 dahil — kullanıcı kararı).

## Notes

- [P] = farklı dosya, bağımsız. Domain testleri impl'den önce (FAIL doğrula).
- Kart verisi: command `InvokeAsync`-only; PAN/CVV saklanmaz/loglanmaz/outbox'a girmez (İlke VI).
- Çift-çekim: 3 banka canlı failover açık pencere; simülatörde bakiye yok→zararsız; gerçek banka öncesi durum-sorgu zorunlu (sonraki versiyon).