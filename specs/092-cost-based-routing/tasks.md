---
description: "Task list — 092 PG Maliyet-Bazlı Routing"
---

# Tasks: PG Maliyet-Bazlı Routing

**Input**: `/specs/092-cost-based-routing/spec.md`

**Tests**: Domain-TDD (İlke VIII) — `BankChargeRouter.SelectCandidates` saf hesap → test-first; Options/appsettings test-sonra.

**Organization**: User story bazlı. MVP = US1 (en düşük maliyetli bankadan başla).

## Path Conventions

Tek Payment BC: `src/services/Payment.Api/`, testler `tests/Payment.Api.Tests/`.

---

## Phase 1: Setup

- [ ] T001 `PosAccountProfile`'a banka+taksit öngörü oran matrisi ekle: `Dictionary<string,decimal> CommissionRates` (taksit→oran) + `decimal DefaultCommissionRate`; statik `CostPriority` kaldırılır (maliyet artık hesaplanır) `src/services/Payment.Api/Options/PosAccountOptions.cs`
- [ ] T002 [P] appsettings.json: her acquirer için `CommissionRates` + `DefaultCommissionRate` (öngörü; MAVI<SARI<TURU örnek), `CostPriority` alanı çıkarılır `src/services/Payment.Api/appsettings.json`

---

## Phase 2: Foundational

**Purpose**: Aday modeli maliyet taşısın — her iki story buna dayanır.

- [ ] T003 `RoutingCandidate`'ı maliyet taşır yap: `CostPriority` yerine `decimal EffectiveCost`; (BankCode, IsOnUs, EffectiveCost) `src/services/Payment.Api/Domains/PaymentLinks/BankCharge/BankChargeRouter.cs`

---

## Phase 3: User Story 1 — En düşük maliyetli uygun bankadan başla (P1) 🎯 MVP

**Goal**: Yetenek elemesi sonrası her aday için efektif maliyet (oran×tutar) hesaplanır, adaylar en düşük maliyetten sıralanır; failover sırayı izler.

**Independent Test**: Farklı oranlı 2+ uygun banka → aday sırası efektif maliyet artan; yetenek-dışı banka aday değil.

- [ ] T004 [US1] `BankChargeRouterTests` (test-first): oran MAVI<SARI → MAVI ilk; yetenek-dışı banka aday değil; bilinmeyen BIN → yalnız maliyet sırası; tanımsız oran → DefaultCommissionRate (aday düşmez) `tests/Payment.Api.Tests/BankChargeRouterTests.cs`
- [ ] T005 [US1] `BankChargeRouter.SelectCandidates` implementasyonu: yetenek eleme (değişmez) → her aday `EffectiveCost = rate(taksit, varsayılan) × tutar` → `OrderBy(EffectiveCost)` (failover bu sırayı izler). Router'a işlem tutarı + taksit girişi geçir (PayPaymentLink handler çağrısı) `src/services/Payment.Api/Domains/PaymentLinks/BankCharge/BankChargeRouter.cs`

**Checkpoint**: En düşük maliyetli uygun bankadan çekim; failover maliyet sırasını izler.

---

## Phase 4: User Story 2 — On-us yalnız eşit maliyette belirleyici (P2)

**Goal**: On-us otomatik öncelik kalkar; eşit maliyette on-us tiebreaker; sıra deterministik.

**Independent Test**: Eşit maliyet + biri on-us → on-us önce; on-us daha pahalı → ucuz olan önce; aynı girdi → aynı sıra.

- [ ] T006 [US2] `BankChargeRouterTests` (test-first): eşit maliyet+on-us → on-us önce; on-us pahalı → ucuz önce (otomatik öncelik YOK); aynı girdi iki kez → birebir aynı sıra `tests/Payment.Api.Tests/BankChargeRouterTests.cs`
- [ ] T007 [US2] Sıralama anahtarı: `OrderBy(EffectiveCost).ThenByDescending(IsOnUs).ThenBy(FallbackOrder).ThenBy(BankCode)` — on-us yalnız eşit maliyette, sonra kararlı fallback (deterministik) `src/services/Payment.Api/Domains/PaymentLinks/BankCharge/BankChargeRouter.cs`

**Checkpoint**: On-us davranışı doğru; sıra tekrarlanabilir.

---

## Phase 5: Polish & Cross-Cutting

- [ ] T008 FLOW.md "Akıllı routing" kuralını güncelle: statik CostPriority + on-us-önce → hesaplanan efektif maliyet-önce, on-us yalnız tiebreaker (domain süreci kuralı değişimi, İlke IX) `src/services/Payment.Api/FLOW.md`
- [ ] T009 `dotnet build` (0) + `dotnet test` (yeni + mevcut 089/090/091 bozulmadan) + `scripts/check-flow-links.sh` yeşil
- [ ] T010 Commit (092 routing); canlı doğrulama (farklı oranlı bankalarla en-ucuz seçimi) sonraki toplu E2E'de

---

## Dependencies & Execution Order

- Setup (T001/T002) → Foundational (T003) → US1 (T004-T005) → US2 (T006-T007) → Polish.
- **US1 bağımsız teslim edilebilir MVP** (en düşük maliyetli routing). US2 on-us tiebreaker'ı ekler (US1'in sıralama anahtarını genişletir — aynı dosya, sıralı).
- `BankChargeRouter.cs` (T003/T005/T007) SIRALI (aynı dosya). appsettings (T002) [P].

## Implementation Strategy

**MVP (US1):** öngörü oran matrisi + efektif maliyet hesabı + en-düşük-maliyet sıralaması. US2 on-us tiebreaker + determinism ekler. Gerçek-zamanlı oran/komisyon tahsili kapsam dışı (öngörü-tabanlı, banka otorite — İlke VII).
