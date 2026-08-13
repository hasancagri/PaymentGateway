---
description: "Task list — Commission Cost + Margin (024)"
---

# Tasks: Commission Cost + Margin

**Input**: Design documents from `/specs/024-commission-cost-margin/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/commission-policy.http.md, quickstart.md

**Tests**: DAHİL — anayasa "Geliştirme Akışı: saf domain birim testi" + research R10 (aritmetik
determinizmi SC-002/SC-005 kanıtı) açıkça ister. Yalnız saf domain birim testleri (DB/HTTP yok).

**Organization**: Tasklar user story'ye göre gruplu; her story bağımsız uygulanıp test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, tamamlanmamış task'a bağımlı değil)
- **[Story]**: US1/US2/US3 (spec.md öncelik P1/P2/P3)
- Dosya yolları tam.

## Path Conventions

BC: `src/services/Commission.Api/` (mevcut genişletilir). Kök: `Domains/CommissionPolicies/`.
Test: `tests/Commission.Api.Tests/` (yeni). Referans desen: 023 `Merchant.Api` + `Merchant.Api.Tests`.

---

## Phase 1: Setup

- [X] T001 [P] `tests/Commission.Api.Tests` xUnit projesini oluştur (net10.0, `Commission.Api`
  referansı; 023 `tests/Merchant.Api.Tests` desenini kopyala) ve `PaymentGateway.slnx`'e ekle
- [X] T002 [P] Yeni komisyon resource sabitlerini ekle (`COMMISSION_POLICY_NOT_ACTIVE`,
  `COMMISSION_POLICY_ALREADY_EXISTS`, `COMMISSION_POLICY_NOT_FOUND`, `COMMISSION_EXCEEDS_PAID_PRICE`)
  — `CommonResourceConstants`'ın bulunduğu dosyaya (src/others/Common; mevcut örüntüyü izle)

---

## Phase 2: Foundational (tüm story'lerin ön koşulu — ÖNCE bitmeli)

- [X] T003 `CommissionPolicyStatus` düz enum (Active/Passive) →
  `src/services/Commission.Api/Domains/CommissionPolicies/CommissionPolicyStatus.cs`
- [X] T004 `CommissionPolicy` aggregate iskeleti (`: AggregateRoot`; private setter alanlar
  `MerchantId`/`Margin`/`Status`/`CreatedAt`/`UpdatedAt`; private ctor; davranış metotları sonra) →
  `.../Domains/CommissionPolicies/CommissionPolicy.cs`  *(T003'e bağlı)*
- [X] T005 `CommissionPolicyEndpointExtension` iskeleti (boş grup map `api/v{version:apiVersion}/commission-policies`,
  `.WithTags("commission-policies")`) → `.../Domains/CommissionPolicies/CommissionPolicyEndpointExtension.cs`
- [X] T006 Program.cs'te grup extension'ı map et (`AddCommissionPolicyGroupEndpointExtension(apiVersionSet)`
  çağrısı; `app.Build()` sonrası, `apiVersionSet` kurulumundan sonra) →
  `src/services/Commission.Api/Program.cs`  *(T005'e bağlı)*

**Checkpoint**: derlenir, `commission-policies` grubu boş map'li; aggregate tipi tüm story'lere hazır.

---

## Phase 3: User Story 1 — Yönetici marj politikası tanımlar (P1) 🎯 MVP

**Goal**: Admin bir merchant için gateway marjını (oran + sabit ücret) oluşturur/günceller/statü
değiştirir; tekil-aktif kuralı. **Independent test**: quickstart S1 (create/duplicate/geçersiz marj).

- [X] T007 [P] [US1] `MarginRule` VO (`RatePercent`/`FixedFee`; `MaxRatePercent=0.20m`/`MaxFixedFee=100m`
  sabitleri; `Create` doğrulama: negatif + cap aşımı `Error`, FR-004) →
  `.../Domains/CommissionPolicies/ValueObjects/MarginRule.cs`
- [X] T008 [US1] Aggregate davranışları — `Create` (boş-Guid reddi + `MarginRule.Create`, Active
  doğar), `UpdateMargin`, `Activate`/`Deactivate` (idempotent no-op) — hepsi `ResultDomain` sarılı
  (014) + `<summary>`/`<remarks>Handler:</remarks>` notlu (015) → `.../CommissionPolicy.cs`
  *(T004, T007'ye bağlı)*
- [X] T009 [US1] `CreateCommissionPolicy` command slice (record+Response+`[Transactional]`Handler+endpoint;
  tekil-aktif FR-005 handler-sorgusu `session.Query<CommissionPolicy>` Active kontrolü;
  `AdminPlaneOnly`+`commission.write`) → `.../Features/Commands/CreateCommissionPolicy.cs`  *(T008)*
- [X] T010 [US1] `UpdateCommissionPolicyMargin` command slice (merchantId'den aktif politikayı bul,
  `UpdateMargin`; not-found `COMMISSION_POLICY_NOT_FOUND`; `AdminPlaneOnly`+`commission.write`) →
  `.../Features/Commands/UpdateCommissionPolicyMargin.cs`  *(T008)*
- [X] T011 [US1] `ChangeCommissionPolicyStatus` command slice (status enum parse → `Activate`/`Deactivate`;
  `AdminPlaneOnly`+`commission.write`) → `.../Features/Commands/ChangeCommissionPolicyStatus.cs`  *(T008)*
- [X] T012 [US1] `ListCommissionPolicies` query slice (admin genel bakış; opsiyonel `?merchantId=`/`?status=`
  filtre; `AdminPlaneOnly`+`commission.read`) → `.../Features/Queries/ListCommissionPolicies.cs`  *(T004)*
- [X] T013 [US1] US1 uçlarını `CommissionPolicyEndpointExtension`'a bağla (Create/Update/Status/List) →
  `.../CommissionPolicyEndpointExtension.cs`  *(T009–T012, T005)*
- [X] T014 [P] [US1] `MarginRuleTests` (negatif oran/ücret reddi, cap aşımı, geçerli değer) →
  `tests/Commission.Api.Tests/MarginRuleTests.cs`  *(T007)*
- [X] T015 [P] [US1] `CommissionPolicyTests` (Create geçerli/boş-Guid, UpdateMargin, statü makinesi
  + aynı-statü idempotent no-op) → `tests/Commission.Api.Tests/CommissionPolicyTests.cs`  *(T008)*

**Checkpoint**: US1 bağımsız çalışır — admin politika CRUD + statü + liste; MVP teslim edilebilir.

---

## Phase 4: User Story 2 — Efektif komisyon hesabı (P2)

**Goal**: Verili işlem bağlamı için efektif komisyon + net hakediş. **Independent test**: quickstart
S2/S4 (bilinen marj+iyzico maliyeti → beklenen aritmetik; tutarsızlık reddi).

- [X] T016 [P] [US2] `EffectiveCommission` VO/hesap-sonucu (`PaidPrice`/`Installment`/`IyzicoCost`/
  `GatewayMargin`/`TotalEffectiveCommission`/`NetPayout`) → `.../ValueObjects/EffectiveCommission.cs`
- [X] T017 [US2] Aggregate'e `CalculateEffectiveCommission(decimal paidPrice, string iyzicoCommission,
  string iyzicoFee, int installment)` ekle — algoritma (data-model): not-active reddi (FR-008/003),
  `decimal.TryParse` InvariantCulture (FR-012), margin `Round(...,2,AwayFromZero)` (R3), efektif>PaidPrice
  reddi (FR-009); `ResultDomain<EffectiveCommission>` → `.../CommissionPolicy.cs`  *(T004, T007, T016)*
- [X] T018 [US2] `CalculateEffectiveCommission` query slice (POST `/effective-commission`; merchantId'den
  aktif politikayı handler-lookup, yoksa `COMMISSION_POLICY_NOT_FOUND`; `AdminPlaneOnly`+`commission.read`;
  `[Transactional]` YOK) → `.../Features/Queries/CalculateEffectiveCommission.cs`  *(T017)*
- [X] T019 [US2] US2 ucunu `CommissionPolicyEndpointExtension`'a bağla →
  `.../CommissionPolicyEndpointExtension.cs`  *(T018, T013)*
- [X] T020 [P] [US2] `EffectiveCommissionTests` (aritmetik+yuvarlama SC-002; not-active; ayrıştırılamaz
  maliyet; efektif>PaidPrice reddi SC-005; net hakediş) →
  `tests/Commission.Api.Tests/EffectiveCommissionTests.cs`  *(T017)*

**Checkpoint**: US1 + US2 çalışır — politika + efektif komisyon dökümü.

---

## Phase 5: User Story 3 — Merchant kendi oranını görür (P3)

**Goal**: Merchant yalnız kendi politikasını görür. **Independent test**: quickstart S5 (self GET
200; çapraz merchant 403).

- [X] T021 [US3] `GetCommissionPolicy` query slice (GET `/{merchantId}`; merchant'ın politikasını döner,
  yoksa `COMMISSION_POLICY_NOT_FOUND`; MerchantKey/sır sızdırmaz; `MerchantScoped`+`commission.read`) →
  `.../Features/Queries/GetCommissionPolicy.cs`  *(T004)*
- [X] T022 [US3] US3 ucunu `CommissionPolicyEndpointExtension`'a bağla →
  `.../CommissionPolicyEndpointExtension.cs`  *(T021, T013)*

**Checkpoint**: 3 story tamam — tam feature.

---

## Phase 6: Polish & Cross-Cutting

- [X] T023 `GlobalUsings.cs`'i gerekiyorsa güncelle (yeni namespace'ler
  `Commission.Api.Domains.CommissionPolicies[.ValueObjects/.Features...]`) →
  `src/services/Commission.Api/GlobalUsings.cs`
- [X] T024 [P] `dotnet build` (0 hata) + `dotnet test tests/Commission.Api.Tests` (yeşil) doğrula
- [ ] T025 [P] Quickstart S1–S5 elle canlı doğrulama (Aspire ayakta; admin + merchant token) — spec kanıtı

---

## Dependencies & Execution Order

- **Setup (T001–T002)** → **Foundational (T003–T006)** → user story'ler.
- **Foundational bloklar**: T003→T004; T004 tüm slice'ların ön koşulu; T005→T006.
- **US1 (P1, MVP)**: T007→T008→(T009/T010/T011/T012)→T013; testler T014(←T007)/T015(←T008).
- **US2 (P2)**: T016 + T007(margin) → T017 → T018 → T019; test T020(←T017). US1'den bağımsız
  uygulanabilir ama endpoint-extension dosyası US1 ile paylaşıldığından T019, T013'ten sonra.
- **US3 (P3)**: T021→T022 (T004'e bağlı; US1/US2'den bağımsız, endpoint kaydı T013 sonrası).
- **Polish (T023–T025)**: tüm story'lerden sonra.
- **Endpoint-extension dosyası (T013/T019/T022) TEK dosya** → bu üç task sıralı (paralel değil).
- **Aggregate dosyası (T008/T017) TEK dosya** → sıralı.

## Parallel Opportunities

- Setup: **T001 ∥ T002**.
- US1 içi: **T007 ∥** (VO ayrı dosya); testler **T014 ∥ T015** (kendi bağımlıları bitince);
  slice'lar T009/T010/T011/T012 ayrı dosya → aggregate (T008) bitince **paralel yazılabilir**
  (yalnız endpoint kaydı T013 hepsini bekler).
- US2 içi: **T016 ∥** (VO); test **T020 ∥** (T017 sonrası).
- Polish: **T024 ∥ T025**.

## Implementation Strategy

- **MVP = US1** (T001–T015): admin marj politikası CRUD + statü + liste + tekil-aktif + birim
  testleri. Tek başına merchant fiyatlandırmasını kurar (spec: "MVP budur").
- **Artımlı**: US1 → US2 (efektif komisyon hesabı, asıl iş değeri) → US3 (merchant şeffaflık).
- Her story kendi checkpoint'inde bağımsız test edilebilir (quickstart S1 / S2+S4 / S5).
- iyzico `Provider`/`Payouts`/`TransactionReports` UYUR — hiçbir task dokunmaz.

**Toplam: 25 task** — Setup 2, Foundational 4, US1 9, US2 5, US3 2, Polish 3.