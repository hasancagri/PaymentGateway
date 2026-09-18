# Tasks: MCP-Only Admin Düzlemi

**Input**: `specs/044-mcp-only-admin-plane/` — plan.md, spec.md, research.md, data-model.md,
contracts/, quickstart.md

**Tests**: Saf domain değişikliği YOK → domain-TDD tetiklenmez (İLKE VI). Handler/tool testleri
test-sonra; canlı doğrulama quickstart senaryolarıyla.

**Organization**: Görevler user story bazlı; her story bağımsız test edilebilir. Söküm (US4)
US1-US3 canlı doğrulanmadan BAŞLAMAZ (FR-008).

## Phase 1: Setup

**Purpose**: Tool adı sabitleri — tüm story'lerin ortak ön koşulu.

- [X] T001 `src/others/Shared/McpToolNames.cs`'e sabitleri ekle:
      `MerchantAdminTools.GetMerchant`/`UpdateMerchant` (admin_get_merchant,
      admin_update_merchant) + `CommissionAdminTools.CreatePolicy`/`UpdateMargin`/`ChangeStatus`
      (admin_create_commission_policy, admin_update_commission_margin,
      admin_change_commission_status)

**Checkpoint**: Sabitler hazır — story'ler başlayabilir.

---

## Phase 2: Foundational

Yok — Merchant.Api ScopeAuthorizationMiddleware 043'ten hazır; Commission middleware taşıması
yalnız US3'ü bloklar (T012, o fazda). US1/US2/US3 T001 sonrası paralel başlayabilir.

---

## Phase 3: User Story 1 - Merchant yönetimi tamamen sohbetten (Priority: P1) 🎯 MVP

**Goal**: `admin_get_merchant` + `admin_update_merchant` — hassas-dışı merchant yönetimi
ekransız. **Independent Test**: quickstart Senaryo 1.

- [X] T002 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Agents/Queries/AdminGetMerchant.cs`
      — `[RequiredScope(MerchantAdmin)]` query + handler + MCP wrapper (aynı dosya). Yanıt:
      MerchantId, Status, Type, Name, Address, ContactName, ContactSurname, TaxOffice,
      LegalCompanyTitle, CreatedTime — hassas/sır alan SÖZLEŞMEDE YOK
      (contracts/merchant-admin-mcp-tools.md). NotFound → RECORD_NOT_FOUND.
- [X] T003 [P] [US1] `src/services/Merchant.Api/Domains/Merchants/Features/Agents/Commands/AdminUpdateMerchant.cs`
      — `[Transactional]` + `[RequiredScope(MerchantAdmin)]`; girdi yalnız hassas-dışı alanlar;
      aggregate yüklenir, `UpdateDetails` hassas alanlar MEVCUT değerlerden geçirilerek çağrılır
      (research R3); yanıt T002 alan seti. Geçersiz Type → INVALID_VALUE.
- [ ] T004 [US1] quickstart Senaryo 1'i canlı çalıştır (Aspire + Claude desktop) — liste/detay/
      güncelleme sohbetten; hassas alan hiçbir yanıtta yok; negatif: bilinmeyen Id → SC-001/SC-002 kanıtı

**Checkpoint**: MVP — merchant hassas-dışı yönetimi tamamen ekransız.

---

## Phase 4: User Story 2 - Kişisel veri agent kanalından geçmez (Priority: P2)

**Goal**: Mevcut tool yanıtlarından Email+Gsm kırpma + tek hassas-veri ekranı (dar BFF çifti).
**Independent Test**: quickstart Senaryo 2.

- [X] T005 [P] [US2] `src/services/Merchant.Api/Domains/Merchants/Features/Agents/Queries/AdminGetMerchants.cs`
      — `MerchantItem`'dan Email + GsmNumber alanlarını ve map'lemelerini SİL (FR-003)
- [X] T006 [P] [US2] `src/services/Merchant.Api/Domains/RegisterRequests/Features/Agents/Queries/AdminGetPendingRegistrations.cs`
      — yanıt öğesinden Email + GsmNumber sil; kimlik belirleyen başka alan varsa birlikte
      değerlendir (contracts/merchant-admin-mcp-tools.md)
- [X] T007 [P] [US2] `src/services/Merchant.Api/Domains/Merchants/Features/Queries/GetMerchantSensitive.cs`
      — GET `/merchants/{merchantId}/sensitive`; `MerchantRead` + `AdminPlaneOnly`; yanıt:
      MerchantId, Name, Type, Email, GsmNumber, IdentityNumber, Iban, TaxNumber
      (contracts/admin-sensitive-endpoint.md); endpoint-extension aynı dosyada
- [X] T008 [P] [US2] `src/services/Merchant.Api/Domains/Merchants/Features/Commands/UpdateMerchantSensitive.cs`
      — PUT `/merchants/{merchantId}/sensitive`; `[Transactional]`, `MerchantWrite` +
      `AdminPlaneOnly`; `UpdateDetails` hassas-DIŞI alanlar mevcut değerlerden geçirilir (R3)
- [X] T009 [US2] `src/services/Merchant.Api/Domains/Merchants/MerchantEndpointExtension.cs` —
      sensitive get/put endpoint'lerini gruba ekle (T007/T008 sonrası; mevcut kayıtlar bu fazda
      DURUR, söküm US4'te)
- [X] T010 [US2] Admin UI hassas-veri sayfası: `src/ui/Admin/Pages/Merchants/Sensitive.cshtml(.cs)`
      (rota `/Merchants/Sensitive?merchantId=`); `Clients/MerchantApiClient.cs`'e sensitive
      get/put metotları; `Pages/Shared/_Layout.cshtml` menü linki. Bulunamayan merchant →
      anlaşılır mesaj (edge case)
- [ ] T011 [US2] quickstart Senaryo 2'yi canlı çalıştır — tool yanıtlarında Email/Gsm yok;
      hassas düzenleme yalnız ekrandan uçtan uca; `admin_get_merchant`'ta Email hâlâ yok — SC-003 kanıtı

**Checkpoint**: Kişisel veri MCP'den akmıyor; hassas yönetim tek ekranda.

---

## Phase 5: User Story 3 - Komisyon politikası yönetimi sohbetten (Priority: P3)

**Goal**: Commission yazma tool'ları + scope middleware taşıması. **Independent Test**:
quickstart Senaryo 3. US1/US2'den bağımsız, T001 sonrası paralel yürüyebilir.

- [X] T012 [US3] 043 Wolverine `ScopeAuthorizationMiddleware` desenini Commission.Api'ye taşı
      (`src/services/Commission.Api/Auth/` + `Program.cs` wiring; Merchant.Api'deki uygulama
      referans) — yazma tool'larını `[RequiredScope]` ile korumanın ön koşulu (research R4;
      bilinçli tekrar, Common'a ÇIKARILMAZ)
- [X] T013 [P] [US3] `src/services/Commission.Api/Domains/CommissionPolicies/Features/Agents/Commands/AdminCreateCommissionPolicy.cs`
      — `[Transactional]` + `[RequiredScope(CommissionWrite)]`; girdi merchantId + tiers;
      tekil-aktif kuralı handler sorgusuyla (024 deseni); `CommissionPolicy.Create`
      (contracts/commission-admin-mcp-tools.md)
- [X] T014 [P] [US3] `.../Agents/Commands/AdminUpdateCommissionMargin.cs` — tam kademe seti
      alır, `CommissionPolicy.UpdateMargin`; politika yoksa NotFound
- [X] T015 [P] [US3] `.../Agents/Commands/AdminChangeCommissionStatus.cs` — status string
      parse + `CommissionPolicy.ChangeStatus`; geçersiz değer/geçiş Result hatası
- [ ] T016 [US3] quickstart Senaryo 3'ü canlı çalıştır — oluştur/marj/statü sohbetten; negatif:
      ikinci aktif politika duplicate — SC-001 kanıtı

**Checkpoint**: Komisyon tam yaşam döngüsü ekransız.

---

## Phase 6: User Story 4 - Eski admin yüzeyinin sökümü (Priority: P4)

**Goal**: Admin-düzlemi REST + Admin CRUD sayfaları + ölü client'lar gider; MerchantScoped
uçlar + makine kanalları KALIR (FR-009/FR-011).

**⚠️ CRITICAL**: T004 + T011 + T016 (canlı doğrulamalar) GEÇMEDEN başlama — geçiş penceresinde
sistem işlevsiz kalmaz (FR-008).

- [X] T017 [US4] Merchant REST sökümü: `Features/Commands/{CreateMerchant,UpdateMerchant,ChangeMerchantStatus}.cs`
      + `Features/Queries/ListMerchants.cs` SİL; `MerchantEndpointExtension.cs` yalnız
      `GetMerchant` (MerchantScoped) + sensitive çiftini map'ler
- [X] T018 [US4] RegisterRequests REST sökümü: `Features/Commands/{ApproveRegisterRequest,RejectRegisterRequest}.cs`
      + `Features/Queries/ListRegisterRequests.cs` + `RegisterRequestEndpointExtension.cs` SİL;
      `Program.cs`'ten `AddRegisterRequestGroupEndpointExtension` kaldır (aktivasyon redeem
      AYRI grupta — DOKUNMA)
- [X] T019 [US4] Commission REST sökümü: `Features/Commands/{CreateCommissionPolicy,UpdateCommissionPolicyMargin,ChangeCommissionPolicyStatus}.cs`
      + `Features/Queries/{ListCommissionPolicies,CalculateEffectiveCommission}.cs` SİL (R2 —
      aggregate metodu KALIR); `CommissionPolicyEndpointExtension.cs` yalnız `GetCommissionPolicy`
- [X] T020 [US4] Admin UI sökümü: `Pages/Merchants/{Index,Details}.*` + `Pages/CommissionPolicies/`
      + `Clients/{CommissionPolicyApiClient,RegisterRequestApiClient}.cs` SİL;
      `MerchantApiClient` yalnız sensitive get/put; `_Layout.cshtml` menü sadeleştir;
      `Clients/ApiModels.cs`'ten ölü modelleri ayıkla
- [X] T021 [US4] Identity ölü client sökümü: `Config.cs`'ten `merchant-agent` + `payment-agent`
      seed'leri + `appsettings.json` `Clients:` secret'ları SİL (R5); `admin-ui` +
      `external-admin-agent` + diğerleri DURUR
- [X] T022 [US4] Silinen slice'lara referanslı test/kod artıklarını temizle — çözüm genelinde
      derleme hatası taraması (`dotnet build`), kalan ölü using/kayıt yok
- [ ] T023 [US4] quickstart Senaryo 4'ü canlı çalıştır — sökülen uçlar 404; MerchantScoped
      uçlar + redeem çalışır; ölü client token alamaz; build+test 0 hata — SC-004 kanıtı

**Checkpoint**: Admin yüzeyi yalnız MCP + tek hassas-veri sayfası.

---

## Phase 7: Polish & Cross-Cutting

**Purpose**: İLKE VII (FLOW.md) + repo belgeleri — bunlar olmadan feature TAMAMLANMIŞ sayılmaz.

- [X] T024 [P] `src/services/Merchant.Api/FLOW.md` — admin sürecinde REST→MCP geçişi,
      CreateMerchant yolunun silinmesi (doğuş yalnız başvuru onayı), hassas-veri BFF adımı;
      kenar-anchor'lar yeni sınıf adlarını gösterir
- [X] T025 [P] `src/services/Commission.Api/FLOW.md` — politika yaşam döngüsünün MCP'ye
      taşınması; silinen REST/Calculate adımları çıkar
- [X] T026 [P] `src/others/Identity.Server/FLOW.md` — ölü `merchant-agent`/`payment-agent`
      client'larının silindiği not düşülür
- [X] T027 CLAUDE.md güncelle — BC haritası + Merchant/Commission/Admin satırlarında 044 durumu
      (MCP-only admin, hassas-veri ekranı, sökümler); `specs/044-mcp-only-admin-plane` origin
      referansı gerekiyorsa ekle
- [X] T028 `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` çalıştır —
      anchor/spec-yolu drifti yok
- [X] T029 `dotnet build` + `dotnet test` tüm çözüm — sıfır hata, sıfır regresyon (SC-005 son kanıt)

---

## Dependencies & Execution Order

- **Setup (P1)**: T001 her şeyi önceler (sabitler).
- **US1 (P3)**: T001 sonrası; T002/T003 paralel, T004 ikisinden sonra.
- **US2 (P4)**: T001 sonrası, US1'den bağımsız; T005-T008 tam paralel; T009 T007+T008'den,
  T010 T009'dan, T011 hepsinden sonra.
- **US3 (P5)**: T001 sonrası, US1/US2'den bağımsız (farklı BC); T012 önce, T013-T015 paralel,
  T016 sonda.
- **US4 (P6)**: T004+T011+T016 GEÇMİŞ olmalı; T017-T021 paralel (farklı dosya kümeleri),
  T022 hepsinden sonra, T023 sonda.
- **Polish (P7)**: US4 sonrası; T024/T025/T026 paralel.

### Parallel Fırsatlar

- T002 ‖ T003 (iki ayrı dosya, aynı desen)
- T005 ‖ T006 ‖ T007 ‖ T008 (dört ayrı dosya)
- US3 tamamı (T012-T016) US1+US2 ile paralel — farklı BC/oturum
- T017 ‖ T018 ‖ T019 ‖ T020 ‖ T021 (farklı servis/proje dosyaları)
- T024 ‖ T025 ‖ T026 (üç FLOW.md)

## Implementation Strategy

1. T001 → US1 (T002-T004) → **MVP**: merchant yönetimi ekransız.
2. US2 (kişisel veri kapatma + hassas ekran) → US3 (komisyon) — ikisi paralel de yürüyebilir.
3. Üç canlı doğrulama (T004/T011/T016) geçince US4 söküm, sonra Polish.
4. Her checkpoint'te `dotnet build` + `dotnet test`.

## Notes

- Her tool = kendi dosyasında record+handler+MCP wrapper (VSA "bir feature = bir dosya");
  Commands/Queries'e IMessageBus ile bile gidilmez (bilinçli tekrar).
- Aggregate metotlarına (`UpdateDetails`, `Create`, `UpdateMargin`, `ChangeStatus`) DOKUNULMAZ.
- Hassas alan adları hiçbir MCP sözleşmesine (girdi/çıktı/description) yazılmaz — kırpma alan
  silmedir, null/boş döndürme değil.
- MerchantScoped `GetMerchant` + `GetCommissionPolicy` ve aktivasyon redeem KESİNLİKLE silinmez
  (FR-011/FR-009).