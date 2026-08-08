---
description: "Task list — Domain Sonuç Sarmalama Standardı (PaymentGateway)"
---

# Tasks: Domain Sonuç Sarmalama Standardı (ResultDomain) — PaymentGateway

**Input**: `/specs/014-domain-result-standard/` (plan.md, spec.md, research.md, data-model.md, quickstart.md)

**Tests**: Mevcut domain birim testleri güncellenir (yeni test suite eklenmez). TDD istenmedi.

**Organization**: User story bazlı. US1 = sonuç sarmalama (P1), US2 = CLAUDE.md kural (P1),
US3 = klasör uyumu (P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralel (farklı dosya, bağımsız)
- Her wrapping task'ı build'i yeşil bırakır: imza + TÜM çağıranlar + testler tek task'ta.

## Path Conventions

Çok-BC mikroservis: `src/services/<Bc>.Api/`, `tests/<Bc>.Api.Tests/` repo kökünde.

---

## Phase 1: Setup

- [X] T001 Baseline doğrula: `dotnet build` 0 hata + `dotnet test tests/Merchant.Api.Tests` yeşil
  (refactor öncesi referans; kırıksa önce onar).

## Phase 2: Foundational

- [X] T002 `ResultDomain` API'sini teyit et (`src/others/Common/Results/ResultDomain.cs`):
  `Ok()`, `Ok(T)`, `Error(List<MessageItem>)`, `Error(MessageItem)` mevcut — yeni tip GEREKMEZ.
  (Blocking değil; sadece referans doğrulama.)

---

## Phase 3: User Story 1 — Tek tip domain sonuç sözleşmesi (P1)

**Goal**: Handler'dan çağrılan 5 ham-dönen metot `ResultDomain`/`ResultDomain<T>` döner; çağıranlar
`IsSuccess/Data/Messages` desenine geçer.

**Independent Test**: `dotnet build` 0 hata + Merchant testleri yeşil + quickstart Adım 3 grep boş.

- [X] T003 [US1] `Merchant.TryActivate()` → `ResultDomain` yap: `src/services/Merchant.Api/Domains/Merchants/Merchant.cs:181`.
  3 koşul sağlanınca `ResultDomain.Ok()`, sağlanmayınca `ResultDomain.Error(...)` (uygun `MessageItem`).
  Çağıranları güncelle: `Merchants/Features/Commands/SetReturnUrl.cs:37`,
  `SettlementAccounts/Features/Commands/CreateSettlementAccount.cs:69`,
  `ReadModels/MerchantCommissionGridReadyHandler.cs:33` (idempotent: `Error` akışı çağırana göre
  yok say veya taşı — davranış eşdeğer kalmalı). Testleri güncelle:
  `tests/Merchant.Api.Tests/MerchantOnboardingTests.cs:54,66,81,87` (`IsSuccess` assertion).
- [X] T004 [US1] `DomainControlChallenge` fabrikası + doğrulaması:
  `Domains/DomainControlChallenges/DomainControlChallenge.cs` —
  `Issue()` (:28) → `ResultDomain<DomainControlChallenge>.Ok(...)`;
  `Verify()` (:45) → `ResultDomain<ChallengeOutcome>.Ok(outcome)` (Karar 2, enum veri olarak).
  Çağıranları güncelle: `RegisterRequests/Features/Agent/SubmitRegistration.cs:85` (Issue),
  `:93` (Verify → `.Data!` outcome). Testleri güncelle:
  `tests/Merchant.Api.Tests/DomainControlChallengeTests.cs` (Issue `.Data`, Verify `.Data` outcome).
- [X] T005 [US1] `ActivationTicket.Issue()` → `ResultDomain<ActivationTicket>.Ok(...)`:
  `Domains/ActivationTickets/ActivationTicket.cs:23`. Çağıranı güncelle:
  `RegisterRequests/Features/Commands/ApproveRegisterRequest.cs:58`. Testleri güncelle:
  `tests/Merchant.Api.Tests/ActivationTicketTests.cs:9,18,32,44` (`.Data`).
- [X] T006 [US1] `OnboardingNotification.Create()` → `ResultDomain<OnboardingNotification>.Ok(...)`:
  `Domains/OnboardingNotifications/OnboardingNotification.cs:22`. Çağıranları güncelle:
  `RegisterRequests/Features/Agent/SubmitRegistration.cs:138`,
  `RegisterRequests/Features/Commands/ApproveRegisterRequest.cs:81`. (Test yok — yeni test zorunlu değil.)
- [X] T007 [US1] US1 doğrula: `dotnet build` 0 hata + `dotnet test tests/Merchant.Api.Tests` 0 başarısız
  + quickstart Adım 3 grep (`bool TryActivate|ChallengeOutcome Verify`) boş.

> Not: T004 ve T006 `SubmitRegistration.cs`'i, T005 ve T006 `ApproveRegisterRequest.cs`'i paylaşır →
> bu üçü [P] DEĞİL, sıralı yürüt. T003 bağımsız dosya kümesi.

---

## Phase 4: User Story 2 — Yazılı kod standardı (P1)

**Goal**: 3 kural `CLAUDE.md`'ye örnekli + muafiyetli yazılır.

**Independent Test**: `CLAUDE.md` üç maddeyi de içerir (metin denetimi).

- [X] T008 [US2] `CLAUDE.md`'ye "Kod standartları" bölümü ekle (mevcut "Yapı ve kurallar" altına):
  (1) **Sonuç sözleşmesi**: handler'dan çağrılan aggregate davranış/fabrika metotları
  `ResultDomain`/`ResultDomain<T>` döner (void mutator dahil); saf getter/sorgu muaf; outcome-enum
  `Ok(outcome)`; örnek `Verify`. (2) **Aggregate-klasör**: `Domains/` hemen altı = tek AggregateRoot;
  iç içe yok; `SharedKernel`/domain-service/seeder/MCP istisna. (3) **ValueObjects**: standalone VO →
  `<Aggregate>/ValueObjects/`. Örnekleri Merchant BC'den ver (`DomainControlChallenge.Verify`,
  `MerchantDescriptor`).

---

## Phase 5: User Story 3 — Klasör düzeni uyumu (P2)

**Goal**: `Domains/` her klasör tek AggregateRoot; standalone VO `ValueObjects/` altında.

**Independent Test**: Her `Domains/<X>/` en fazla bir `: AggregateRoot`; build yeşil.

- [X] T009 [P] [US3] Doğrula: PaymentGateway'de her `Domains/<X>/` tek AggregateRoot içerir
  (Merchant BC bu sprint öncesi düzeltildi). Tarama:
  `grep -rlE "class .*: AggregateRoot" src/*/*/Domains` → her klasör tek dosya. İhlal çıkarsa
  aggregate'i kendi klasörüne taşı (git mv + namespace + GlobalUsings + çağıranlar).
- [X] T010 [P] [US3] Standalone VO taraması: aggregate kökünde duran value object (class/record,
  AggregateRoot değil) → ilgili `ValueObjects/` altına taşı. (PG'de bilinen ihlal yok — envanter
  raporlamadı; yalnız doğrulama + varsa taşıma.)

---

## Phase 6: Polish & Cross-Cutting

- [X] T011 Tüm çözüm doğrula: `dotnet build` 0 hata + `dotnet test tests/Merchant.Api.Tests` +
  `dotnet test tests/Commission.Api.Tests` 0 başarısız.
- [X] T012 Quickstart senaryolarını çalıştır (`quickstart.md` Adım 1-3) ve SC-001..005 eşlemesini
  işaretle.
- [ ] T013 Commit: `refactor(domain): handler-çağrılı domain metotları ResultDomain'e sarıldı (014)`.

---

## Dependencies

- Phase 1 → 2 → 3 (US1) → 6. US2 (Phase 4) ve US3 (Phase 5) US1'den bağımsız, US1 sonrası veya
  paralel yürüyebilir (farklı dosyalar: CLAUDE.md / klasör). 
- US1 içi: T003 [P] (bağımsız); T004→T005→T006 sıralı (paylaşılan handler dosyaları); T007 gate.

## Parallel Opportunities

- T003 ile (T004 başlamadan) T008 (CLAUDE.md) ve T009/T010 (klasör) paralel yürütülebilir — ayrı dosya.
- T009 [P] + T010 [P] birbirinden bağımsız.

## MVP Scope

**US1 (Phase 3)** tek başına MVP: sonuç sözleşmesi tek tipleşir, build+test yeşil. US2 (dokümantasyon)
ve US3 (klasör doğrulama) tamamlayıcı.
