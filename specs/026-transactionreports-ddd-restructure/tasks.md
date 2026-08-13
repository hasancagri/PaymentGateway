---
description: "Task list — TransactionReports Yapısal DDD Geçişi (026)"
---

# Tasks: TransactionReports Yapısal DDD Geçişi

**Input**: Design documents from `/specs/026-transactionreports-ddd-restructure/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: YENİ test YOK — yapısal geçiş davranış eklemez (FR-004/SC-005). Doğrulama = grep +
`dotnet build` + MEVCUT `tests/Commission.Api.Tests` (20/20, FR-006/SC-003).

**Organization**: 025 SubMerchants deseninin birebir tekrarı, Commission.Api'de.

## Format: `[ID] [P?] [Story] Description`

## Path Conventions

BC: `src/services/Commission.Api/`. Kaynak: `Domains/TransactionReports/` → hedef
`Provider/Reporting/` (namespace `Commission.Api.Provider.Reporting`). "Taşı" = `git mv` +
namespace satırı güncelle. Domain (`Domains/CommissionPolicies/`, `Domains/Payouts/`) DOKUNULMAZ.

---

## Phase 1: Setup

- [X] T001 Güvenli-taşıma teyidi: `grep -rn "TransactionReport\|TransactionDetail\|PaymentTxDetailItem\|RefundDetailItem\|ConvertedPayout" --include="*.cs" src/services/Commission.Api | grep -v "Domains/TransactionReports/"`
  ile 13 tipin referanssız olduğunu doğrula (yalnız `GlobalUsings.cs` + `CommissionPolicy.cs` doc-yorum). Referans çıkarsa taşımaya ekle.

---

## Phase 2: Foundational

*(Yok — `Provider/Reporting/` klasörü ilk taşımayla oluşur.)*

---

## Phase 3: User Story 1 — Sağlayıcı/wire tipleri sınıra taşınır (P1) 🎯 MVP

**Goal**: 13 iyzico rapor wire/istemci tipi `Domains/`'den `Provider/Reporting/`'e taşınır.
**Independent test**: quickstart S1 (`Domains/` altında TransactionReports provider-türeyen = 0) + S4 (build/test).

- [X] T002 [US1] 13 dosyayı `git mv src/services/Commission.Api/Domains/TransactionReports/*.cs
  src/services/Commission.Api/Provider/Reporting/` ile taşı (`Provider/Reporting/` oluştur)
- [X] T003 [US1] Taşınan 13 dosyada namespace satırını `Commission.Api.Domains.TransactionReports`
  → `Commission.Api.Provider.Reporting` değiştir (resource+çağrı birleşik desen + nested DTO'lar +
  PKI istekler aynen korunur — R2)  *(T002 sonrası)*
- [X] T004 [US1] `GlobalUsings.cs` güncelle: `global using Commission.Api.Domains.TransactionReports;`
  → `global using Commission.Api.Provider.Reporting;` → `src/services/Commission.Api/GlobalUsings.cs`
  *(T002/T003 sonrası)*

**Checkpoint**: 13 tip Provider/Reporting'de; çözüm derlenir; 024 dokunulmadı.

---

## Phase 4: User Story 2 — Klasör dağıtımı + konvansiyon (P2)

**Goal**: `Domains/TransactionReports/` dağıtılır; aggregate-klasör kuralı korunur.
**Independent test**: quickstart S2 (aggregate-klasör tek-kök) + S1 (klasör yok).

- [X] T005 [US2] `git mv` sonrası boşalan `src/services/Commission.Api/Domains/TransactionReports/`
  klasörünü sil (kalıntı varsa)  *(T002 sonrası)*

**Checkpoint**: `Domains/TransactionReports/` yok; `Domains/` altında TransactionReports provider-
türeyen tip 0.

---

## Phase 5: Polish & Cross-Cutting (doğrulama)

- [X] T006 [P] Yapısal doğrulama (quickstart S1–S3): `Domains/TransactionReports/` yok;
  `Provider/Reporting/` 13 dosya; aggregate-klasör tek-kök (`TransactionReports` yok);
  `Domains/CommissionPolicies/` diff = 0 (SC-005)
- [X] T007 [P] `dotnet build src/services/Commission.Api/Commission.Api.csproj` (0 hata) +
  `dotnet test tests/Commission.Api.Tests` (20/20 yeşil, SC-003/FR-006)

---

## Dependencies & Execution Order

- **T001** → **T002** → **T003** → **T004** → **T005** → **Polish (T006/T007)**.
- T002 (git mv) tüm taşımanın temeli; T003 (namespace) T002 sonrası; T004 (GlobalUsings) T002/T003
  sonrası; T005 (klasör sil) T002 sonrası (git mv zaten dosyaları çıkarır → klasör boş/yok).
- **T006 ∥ T007** — doğrulama.

## Parallel Opportunities

- Taşıma tek `git mv` toplu komut → dosya-başı paralellik yok (tek işlem). Namespace güncelleme (T003)
  tek scripted geçiş.
- **T006 ∥ T007** doğrulama.

## Implementation Strategy

- **MVP = US1** (T001–T004): 13 tip sınıra taşınır — CP.VPOS-sınırı + anemik-in-Domains giderimi.
- **US2** (T005): klasör dağıtımı.
- 024 `CommissionPolicy` + Payouts (ayrı geçiş) DOKUNULMAZ.
- Davranış (canlı rapor çekimi) AYRI spec.

**Toplam: 7 task** — Setup 1, US1 3, US2 1, Polish 2.
