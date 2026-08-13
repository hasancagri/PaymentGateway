---
description: "Task list — Payouts Yapısal DDD Geçişi (027)"
---

# Tasks: Payouts Yapısal DDD Geçişi

**Input**: Design documents from `/specs/027-payouts-ddd-restructure/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: YENİ test YOK — yapısal geçiş davranış eklemez (FR-004/SC-005). Doğrulama = grep +
`dotnet build` + MEVCUT `tests/Commission.Api.Tests` (20/20).

**Organization**: 025/026 deseninin birebir tekrarı. Roadmap son geçişi.

## Format: `[ID] [P?] [Story] Description`

## Path Conventions

BC: `src/services/Commission.Api/`. Kaynak: `Domains/Payouts/` → hedef `Provider/Payout/` (namespace
`Commission.Api.Provider.Payout`). "Taşı" = `git mv` + namespace satırı. Domain (`Domains/CommissionPolicies/`)
DOKUNULMAZ.

---

## Phase 1: Setup

- [X] T001 Güvenli-taşıma teyidi: `grep -rn "class .*: *(BaseRequestV2|ProviderResourceV2)\|PayoutCompletedTransaction\|BankTransfer\|CrossBooking" --include="*.cs" src/services/Commission.Api | grep -v "Domains/Payouts/"`
  ile 8 tipin referanssız olduğunu doğrula (dış "Payout" yalnız `GlobalUsings.cs` + 024 substring alan adları). Referans çıkarsa taşımaya ekle.

---

## Phase 2: Foundational

*(Yok — `Provider/Payout/` klasörü ilk taşımayla oluşur.)*

---

## Phase 3: User Story 1 — Sağlayıcı/wire tipleri sınıra taşınır (P1) 🎯 MVP

**Goal**: 8 iyzico payout wire/istemci tipi `Domains/`'den `Provider/Payout/`'e taşınır.
**Independent test**: quickstart S1 (`Domains/` sağlayıcı-türeyen = 0) + S4 (build/test).

- [X] T002 [US1] 8 dosyayı `git mv src/services/Commission.Api/Domains/Payouts/*.cs
  src/services/Commission.Api/Provider/Payout/` ile taşı (`Provider/Payout/` oluştur)
- [X] T003 [US1] Taşınan 8 dosyada namespace satırını `Commission.Api.Domains.Payouts` →
  `Commission.Api.Provider.Payout` değiştir (resource+çağrı + nested DTO + PKI istek aynen — R2)
  *(T002 sonrası)*
- [X] T004 [US1] `GlobalUsings.cs` güncelle: `global using Commission.Api.Domains.Payouts;` →
  `global using Commission.Api.Provider.Payout;` → `src/services/Commission.Api/GlobalUsings.cs`
  *(T002/T003 sonrası)*

**Checkpoint**: 8 tip Provider/Payout'ta; çözüm derlenir; 024 dokunulmadı.

---

## Phase 4: User Story 2 — Klasör dağıtımı + konvansiyon (P2)

**Goal**: `Domains/Payouts/` dağıtılır; `Domains/` sağlayıcı-türeyenden TAM arınır.
**Independent test**: quickstart S2 (aggregate-klasör tek-kök) + S1 (klasör yok, Domains temiz).

- [X] T005 [US2] `git mv` sonrası boşalan `src/services/Commission.Api/Domains/Payouts/` klasörünü
  sil (kalıntı varsa)  *(T002 sonrası)*

**Checkpoint**: `Domains/Payouts/` yok; `Domains/` yalnız `CommissionPolicies` içerir.

---

## Phase 5: Polish & Cross-Cutting (doğrulama)

- [X] T006 [P] Yapısal doğrulama (quickstart S1–S3): `Domains/` altında sağlayıcı-türeyen = 0 (TAM);
  `Domains/Payouts/` yok; `Provider/Payout/` 8 dosya; aggregate-klasör tek-kök;
  `Domains/CommissionPolicies/` diff = 0 (SC-005)
- [X] T007 [P] `dotnet build src/services/Commission.Api/Commission.Api.csproj` (0 hata) +
  `dotnet test tests/Commission.Api.Tests` (20/20 yeşil, SC-003/FR-006)

---

## Dependencies & Execution Order

- **T001** → **T002** → **T003** → **T004** → **T005** → **Polish (T006/T007)**.
- **T006 ∥ T007** — doğrulama.

## Parallel Opportunities

- Taşıma tek `git mv` toplu komut. **T006 ∥ T007** doğrulama.

## Implementation Strategy

- **MVP = US1** (T001–T004): 8 tip sınıra taşınır.
- **US2** (T005): klasör dağıtımı — `Domains/` TAM temizlenir.
- 024 `CommissionPolicy` DOKUNULMAZ. Davranış AYRI spec. Roadmap SON geçişi.

**Toplam: 7 task** — Setup 1, US1 3, US2 1, Polish 2.
