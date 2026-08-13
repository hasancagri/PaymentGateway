---
description: "Task list — SubMerchants Yapısal DDD Geçişi (025)"
---

# Tasks: SubMerchants Yapısal DDD Geçişi

**Input**: Design documents from `/specs/025-submerchant-ddd-restructure/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: YENİ test YOK — bu yapısal geçiş davranış eklemez (FR-004/SC-005). Doğrulama = grep +
`dotnet build` + MEVCUT `tests/Merchant.Api.Tests` yeşilliği (regresyon guardrail, FR-006/SC-003).

**Organization**: Tasklar user story'ye göre; her story bağımsız doğrulanabilir (grep/build).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel (farklı dosya)
- **[Story]**: US1/US2 (spec.md P1/P2)

## Path Conventions

BC: `src/services/Merchant.Api/`. Kaynak: `Domains/SubMerchants/` → hedef `Provider/Onboarding/`
(namespace `Merchant.Api.Provider.Onboarding`). "Taşı" = yeni yola oluştur + eski dosyayı sil +
namespace güncelle. Domain (`Domains/Merchants/`) DOKUNULMAZ.

---

## Phase 1: Setup

- [X] T001 Güvenli-taşıma teyidi: `grep -rln SubMerchant src/services/Merchant.Api --include="*.cs"`
  ile SubMerchant TİPLERİNİN referanssız olduğunu doğrula (dış geçişler yalnız `SubMerchantKey`
  alanı + `GlobalUsings.cs`). Referans çıkarsa taşıma sırasına ekle.

---

## Phase 2: Foundational

*(Yok — paylaşılan ön koşul gerekmez; `Provider/Onboarding/` klasörü ilk dosya taşımasıyla oluşur.)*

---

## Phase 3: User Story 1 — Sağlayıcı/wire tipleri sınıra taşınır (P1) 🎯 MVP

**Goal**: iyzico wire/istemci tipleri `Domains/`'den `Provider/Onboarding/`'e taşınır; domain sınırı
netleşir. **Independent test**: quickstart S1 (`Domains/` altında provider-türeyen tip = 0) + S5 (build/test yeşil).

- [X] T002 [P] [US1] `SubMerchant.cs`'i taşı → `src/services/Merchant.Api/Provider/Onboarding/SubMerchant.cs`;
  namespace `Merchant.Api.Domains.SubMerchants` → `Merchant.Api.Provider.Onboarding`
  (ProviderResourceV2 + Create/Update/Retrieve HTTP aynen korunur — R5)
- [X] T003 [P] [US1] `CreateSubMerchantRequest.cs`'i taşı →
  `src/services/Merchant.Api/Provider/Onboarding/CreateSubMerchantRequest.cs`; namespace güncelle
- [X] T004 [P] [US1] `UpdateSubMerchantRequest.cs`'i taşı →
  `src/services/Merchant.Api/Provider/Onboarding/UpdateSubMerchantRequest.cs`; namespace güncelle
- [X] T005 [P] [US1] `RetrieveSubMerchantRequest.cs`'i taşı →
  `src/services/Merchant.Api/Provider/Onboarding/RetrieveSubMerchantRequest.cs`; namespace güncelle
- [X] T006 [US1] `GlobalUsings.cs` güncelle: `global using Merchant.Api.Domains.SubMerchants;`
  satırını kaldır; provider tarafı BC-içi kullanıldığından `global using Merchant.Api.Provider.Onboarding;`
  gerekiyorsa ekle → `src/services/Merchant.Api/GlobalUsings.cs`  *(T002–T005 sonrası)*

**Checkpoint**: 4 wire/istemci tipi Provider'da; `Domains/SubMerchants/` yalnız `SubMerchantType`
kaldı; çözüm derlenir.

---

## Phase 4: User Story 2 — Wire vocab + klasör dağıtımı (konvansiyon) (P2)

**Goal**: `SubMerchantType` wire vocab sınıra taşınır, `Domains/SubMerchants/` dağıtılır; aggregate-
klasör kuralı geri gelir. **Independent test**: quickstart S2 (aggregate-klasör tek-kök) + S3
(SubMerchantType 3 değer korunur).

- [X] T007 [US2] `SubMerchantType.cs`'i taşı → `src/services/Merchant.Api/Provider/Onboarding/SubMerchantType.cs`;
  namespace `...Domains.SubMerchants` → `...Provider.Onboarding`; 3 değer (PERSONAL/PRIVATE_COMPANY/
  LIMITED_OR_JOINT_STOCK_COMPANY) korunur (FR-005/SC-004)
- [X] T008 [US2] `src/services/Merchant.Api/Domains/SubMerchants/` klasörünü sil (tüm tipler taşındı;
  aggregate-klasör kuralı SC-002)  *(T002–T005, T007 sonrası)*

**Checkpoint**: `Domains/SubMerchants/` yok; `Domains/` altında sağlayıcı-türeyen tip 0; SubMerchantType
Provider'da korunur.

---

## Phase 5: Polish & Cross-Cutting (doğrulama)

- [X] T009 [P] Yapısal doğrulama (quickstart S1–S4): `Domains/` altında `BaseRequestV2`/
  `ProviderResourceV2` türeyen = 0; `Domains/SubMerchants/` yok; `Provider/Onboarding/` 5 dosya;
  aggregate-klasör tek-kök; `Merchants/` domain diff yok (davranış eklenmedi, SC-005)
- [X] T010 [P] `dotnet build src/services/Merchant.Api/Merchant.Api.csproj` (0 hata) +
  `dotnet test tests/Merchant.Api.Tests` (yeşil — `Merchant.SubMerchantKey` null assert'leri dahil
  kırılmaz, SC-003/FR-006)

---

## Dependencies & Execution Order

- **Setup (T001)** → **US1 (T002–T006)** → **US2 (T007–T008)** → **Polish (T009–T010)**.
- **US1**: T002–T005 paralel (ayrı dosya); T006 (GlobalUsings) taşımalar sonrası.
- **US2**: T007 paralel-uygun ama T008 (klasör sil) T002–T005 + T007'nin HEPSİNİ bekler (son dosya
  çıkınca).
- **Polish**: tüm taşımalar + GlobalUsings sonrası.
- US2, US1'e mantıken bağlı değil (ayrı dosya) ama klasör dağıtımı (T008) tüm taşımaları bekler →
  pratikte US1 sonrası.

## Parallel Opportunities

- **T002 ∥ T003 ∥ T004 ∥ T005 ∥ T007** — beş dosya taşıma bağımsız (farklı dosyalar). Tek engel:
  T006 (GlobalUsings) ve T008 (klasör sil) bunları bekler.
- **T009 ∥ T010** — doğrulama.

## Implementation Strategy

- **MVP = US1** (T001–T006): wire/istemci tipleri sınıra taşınır — CP.VPOS-sınırı + anemik-in-Domains
  ihlalinin asıl giderimi. Tek başına değer.
- **US2** (T007–T008): wire vocab + klasör dağıtımı — aggregate-klasör kuralını tamamlar.
- Domain (`Domains/Merchants/`) hiç dokunulmaz; 024 Commission alakasız.
- Davranış (canlı iyzico kaydı) AYRI spec — bu iş yalnız yapı.

**Toplam: 10 task** — Setup 1, US1 5, US2 2, Polish 2.
