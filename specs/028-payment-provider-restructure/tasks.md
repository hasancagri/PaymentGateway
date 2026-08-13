---
description: "Task list — Payment.Api iyzico Wire Material Geçişi (028)"
---

# Tasks: Payment.Api iyzico Wire Material — Yapısal DDD Geçişi

**Input**: Design documents from `/specs/028-payment-provider-restructure/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: YENİ test YOK — yapısal geçiş davranış eklemez (FR-005/SC-004). Payment.Api test projesi
yok; doğrulama = grep + `dotnet build` + diğer BC testleri (Merchant 30 + Commission 20).

**Organization**: 025/026/027 deseninin tekrarı; tek spec 3 story (Payments/Installments/StoredCards).

## Format: `[ID] [P?] [Story] Description`

## Path Conventions

BC: `src/services/Payment.Api/`. Kaynak: `Domains/{X}/` → hedef `Provider/{X}/` (namespace
`Payment.Api.Provider.{X}`). "Taşı" = `git mv` + namespace satırı. Payment.Api gerçek domain'i yok →
`Domains/` boşalır.

---

## Phase 1: Setup

- [X] T001 Güvenli-taşıma teyidi: `grep -rn "Payment.Api.Domains.\(Payments\|Installments\|StoredCards\)" --include="*.cs" src | grep -v "Domains/"`
  ile dış tip referansının yalnız `GlobalUsings.cs` (3 satır) olduğunu doğrula. Ekstra referans çıkarsa taşımaya ekle.

---

## Phase 2: Foundational

*(Yok — `Provider/{X}/` klasörleri ilk taşımayla oluşur.)*

---

## Phase 3: User Story 1 — Payments (P1) 🎯 MVP

**Goal**: 28 Payments wire tipi `Provider/Payments/`'e taşınır. **Independent test**: quickstart S1
(`Domains/Payments/` yok) + S3 (build).

- [X] T002 [US1] 28 dosyayı `git mv src/services/Payment.Api/Domains/Payments/*.cs
  src/services/Payment.Api/Provider/Payments/` ile taşı (`Provider/Payments/` oluştur)
- [X] T003 [US1] Taşınan 28 dosyada namespace `Payment.Api.Domains.Payments` →
  `Payment.Api.Provider.Payments` değiştir (resource+çağrı + DTO + enum aynen)  *(T002 sonrası)*
- [X] T004 [US1] Boşalan `Domains/Payments/` klasörünü sil  *(T002 sonrası)*

---

## Phase 4: User Story 2 — Installments (P2)

**Goal**: 6 Installments wire tipi `Provider/Installments/`'e. **Independent test**: quickstart S1
(`Domains/Installments/` yok).

- [X] T005 [US2] 6 dosyayı `git mv src/services/Payment.Api/Domains/Installments/*.cs
  src/services/Payment.Api/Provider/Installments/` ile taşı
- [X] T006 [US2] Taşınan 6 dosyada namespace `...Domains.Installments` → `...Provider.Installments`
  değiştir  *(T005 sonrası)*
- [X] T007 [US2] Boşalan `Domains/Installments/` klasörünü sil  *(T005 sonrası)*

---

## Phase 5: User Story 3 — StoredCards (P3)

**Goal**: 6 StoredCards wire tipi `Provider/StoredCards/`'e; `Domains/` TAM boşalır. **Independent
test**: quickstart S1 (Domains sağlayıcı-türeyen = 0).

- [X] T008 [US3] 6 dosyayı `git mv src/services/Payment.Api/Domains/StoredCards/*.cs
  src/services/Payment.Api/Provider/StoredCards/` ile taşı
- [X] T009 [US3] Taşınan 6 dosyada namespace `...Domains.StoredCards` → `...Provider.StoredCards`
  değiştir  *(T008 sonrası)*
- [X] T010 [US3] Boşalan `Domains/StoredCards/` klasörünü sil  *(T008 sonrası)*

---

## Phase 6: GlobalUsings + Polish (doğrulama)

- [X] T011 `GlobalUsings.cs` güncelle: 3 satır `Payment.Api.Domains.{Payments,Installments,StoredCards}`
  → `Payment.Api.Provider.{X}` → `src/services/Payment.Api/GlobalUsings.cs`  *(T002–T010 sonrası)*
- [X] T012 [P] Yapısal doğrulama (quickstart S1–S2): `Payment.Api/Domains/` sağlayıcı-türeyen = 0;
  3 klasör yok; `Provider/{Payments:28,Installments:6,StoredCards:6}`
- [X] T013 [P] `dotnet build PaymentGateway.slnx` (0 hata) + `dotnet test` Merchant(30)+Commission(20) yeşil (SC-003/FR-006)

---

## Dependencies & Execution Order

- **T001** → US1 (T002→T003→T004) → US2 (T005→T006→T007) → US3 (T008→T009→T010) → **T011 (GlobalUsings, hepsinden sonra)** → **Polish (T012/T013)**.
- Üç story ayrı klasör → mantıken bağımsız; ama GlobalUsings (T011) üç taşımayı da bekler, ve derleme
  yeşilliği ancak T011 sonrası. Pratikte sıralı git mv + tek GlobalUsings güncelleme.
- **T012 ∥ T013** — doğrulama.

## Parallel Opportunities

- Üç `git mv` bağımsız (farklı klasör) ama tek script içinde sıralı yapmak basit.
- **T012 ∥ T013** doğrulama.

## Implementation Strategy

- **MVP = US1** (Payments, en büyük çekirdek). US2/US3 küçük ekler.
- Payment.Api gerçek domain'i yok → `Domains/` TAM boşalır (charge akışı sonraki davranış spec'inde).
- Davranış AYRI spec. Bu, iyzico SDK yapısal roadmap'inin Payment ayağı.

**Toplam: 13 task** — Setup 1, US1 3, US2 3, US3 3, Polish 3.
