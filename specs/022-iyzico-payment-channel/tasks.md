# Tasks: Iyzico Payment Channel — Yapısal Eritme

**Input**: Design documents from `/specs/022-iyzico-payment-channel/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R8), data-model.md (dosya eşlemesi), quickstart.md

**Tests**: Test YAZILMAZ; 5 test projesi SİLİNİR (ölü aggregate/SDK testleri). Kapanış =
build 0 hata + kalıntı taraması 0. Canlı doğrulama YOK (kullanıcı kararı).

**Not**: Kullanıcının elle sildiği Domains dosyaları working tree'de staged — dal bu
değişikliklerin ÜZERİNE açılır, silmeler 022 commit'ine girer.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Mevcut değişikliklerle (staged silmeler dahil) `master`'dan `022-iyzico-payment-channel` dalını oluştur; iCloud " 2" kopyalarını süpür (`find . -name "* 2.*" -delete`, bin/obj hariç kontrol)

## Phase 2: Foundational

*(Görev yok — US'ler doğrudan başlar)*

## Phase 3: User Story 1 - Legacy POS ekseni sökülür (P1) 🎯 MVP

**Goal**: CP.VPOS ve eski eksen kalıntıları tamamen gider.

**Independent Test**: quickstart S2 taramasının CP.VPOS/BankRouter/PosAccount/BinCard kolu 0 satır.

- [X] T002 [US1] `src/services/CP.VPOS/` klasörünü sil; `PaymentGateway.slnx`'ten girdisini, `.gitignore`'dan `src/services/CP.VPOS/` satırını, `src/services/Payment.Api/Payment.Api.csproj`'dan proje referansını çıkar
- [X] T003 [US1] Eski eksen artıklarını tara/temizle: `grep -rniE "BankRouter|PosAccount|BinCard|CP\.VPOS"` (src, specs hariç) — Admin sayfaları/client'ları dahil kalan referansları sil (ör. `src/ui/Admin`'de BinCard/PosAccount sayfaları ve `ApiModels`/client metodları)

## Phase 4: User Story 2 - Sağlayıcı istemcisi BC'lere dağıtılır (P2)

**Goal**: SDK'nın kullanılan yapıları üç BC'nin Domains/Provider alanlarına, BC
namespace'leriyle yerleşir; Iyzipay* projeleri ve kullanılmayan modüller ölür.

**Independent Test**: Çözümde Iyzipay* projesi 0; data-model eşlemesindeki hedef
klasörler dolu; `Iyzipay` namespace'i kaynakta 0 geçer.

- [X] T004 [US2] `src/services/Payment.Api/Provider/` oluştur: data-model "Payment.Api → Provider" satırındaki çekirdek dosyaları kopyala (v1 taban tipleri dahil — `BaseRequest`/`IyzipayResource` Model kökünde ise oradan); namespace'leri `Payment.Api.Provider` yap; `IyzipayConstants` içeriğini nötr adla taşı
- [X] T005 [US2] `src/services/Payment.Api/Domains/{Payments,Installments,StoredCards}/` oluştur ve data-model eşlemesindeki Model/Request dosyalarını taşı; namespace `Payment.Api.Domains.<Alan>`; using'ler `Payment.Api.Provider`'a döner
- [X] T006 [P] [US2] `src/services/Merchant.Api/Provider/` (mini çekirdek kopya) + `Domains/SubMerchants/` (SubMerchant, SubMerchantType, Create/Update/RetrieveSubMerchantRequest); namespace `Merchant.Api.*`
- [X] T007 [P] [US2] `src/services/Commission.Api/Provider/` (çekirdek + BaseRequestV2, IyzipayResourceV2, PagingRequest, ResponseData, ResponsePagingData) + `Domains/Payouts/` + `Domains/TransactionReports/` (data-model eşlemesi); namespace `Commission.Api.*`
- [X] T008 [US2] Üç BC csproj'una sürümsüz `Newtonsoft.Json` PackageReference ekle (CPM 13.0.4); Payment.Api'de artık kullanılmayan referanslar varsa (SharedKernel dahil — tarama sonucu) çıkar
- [X] T009 [US2] Kalan SDK'yı kökten sil: `src/services/Iyzipay/`, `src/services/Iyzipay.Samples/`, `tests/Iyzipay.Tests/`, `tests/Iyzipay.Tests.Functional/` klasörleri + `PaymentGateway.slnx`'ten 4 girdi; `grep -rn "Iyzipay"` (specs hariç) 0 doğrula

## Phase 5: User Story 3 - Payment BC yüzeyi derlenir (P3)

**Goal**: Üç BC + CardVault + çözüm bütünü sıfır hatayla derlenir; ölü test projeleri gider.

**Independent Test**: quickstart S1 (build) + S3 (test koşusu boş-hatasız) + S4 (sınır sızması 0).

- [X] T010 [US3] `src/services/Payment.Api/Program.cs` + `GlobalUsings.cs` onar: silinen tiplerin Marten şemaları, endpoint map'leri, MCP kayıtları, `BinCardSeeder` kaydı, ölü using'ler çıkar; yeni namespace'ler gerekirse eklenir
- [X] T011 [P] [US3] `src/services/Merchant.Api/Program.cs` + `GlobalUsings.cs` onar (kalan şema/map kayıtları); `ReadModels/MerchantCommissionGridReadyHandler.cs` ölü Merchant aggregate'ine bakıyorsa sil
- [X] T012 [P] [US3] `src/services/Commission.Api/Program.cs` + `GlobalUsings.cs` onar (şema/map/MCP kayıtları; Wolverine Shared-event publish kayıtları KALIR)
- [X] T013 [US3] `src/services/Payment.Api/CardVault/` onar: BinCard/`CardInfo` bağı varsa vault-içi minimal tiple değiştir; PAN koruma çekirdeği aynen kalır
- [X] T014 [US3] Ölü BC test projelerini sil: `tests/Payment.Api.Tests/`, `tests/Merchant.Api.Tests/`, `tests/Commission.Api.Tests/` + slnx girdileri; `src/others/SharedKernel` tüketicisiz kaldıysa projeyi + slnx girdisini + referansları sil
- [X] T015 [US3] `dotnet build` sıfır hataya çek (asgari düzeltme; Nullable/ImplicitUsings uyarıları serbest, hata değilse dokunma); quickstart S3-S4 komutlarını koş

## Phase 6: Polish

- [X] T016 [P] `CLAUDE.md` güncelle: CP.VPOS/BinCard/BankRouter/PosAccount/Iyzipay anlatımları çıkar; üç BC'nin 022 ara durumu (Domains = sağlayıcı malzemesi, Provider/ katmanı, uçsuz), test komutlarının kaldırılması, CPM istisnasının bitişi işlensin
- [X] T017 Quickstart S1-S4'ü uçtan uca koş, sonuçları `specs/022-iyzico-payment-channel/quickstart.md` Notlar'a işle; commit (staged kullanıcı silmeleri dahil)

## Uygulama notu (2026-08-13)

Implement sırasında kullanıcı ek silmeler yaptı ve kapsam genişledi:
- **CardVault silindi** (kullanıcı) — spec'in "korunur" maddesi geçersizleşti; PanTools/CardBrand onarımı gereksizleşti.
- **SharedKernel silindi** (kullanıcı talebi) — CardTaxonomy dahil; tek kod bağı (PanTools) CardVault ile birlikte gitti.
- **Excel.Mcp silindi** (kullanıcı; slnx/AppHost'u kendisi temizledi) — Identity seed'inden `document.generate` scope'u çıkarıldı.
- Iyzipay tip adları nötrleştirildi: `IyzipayResourceV2`→`ProviderResourceV2`, `IyzipayConstants`→`ProviderConstants`; `Options`→`ProviderOptions` (Merchant/Commission `Options` namespace çakışması). Protokol değerleri (`iyzipay-dotnet-2.1.78`, `*.iyzipay.com`) bilinçli istisna.
- `RetrievePayoutTransactionsRequest` SDK'da yok (payout sorgusu `RetrieveTransactionsRequest`); `BankTransfer` + `ConvertedPayout` bağımlılık gereği Commission'a eklendi; `LoyaltyReward` CreatePaymentRequest bağımlılığı olarak Payments'a alındı; `InitialConsumer` (Iyziup bağımlısı) atıldı.

## Dependencies

- T001 → T002-T003 (US1) → T004-T009 (US2; T006 ‖ T007, T004-T005 sıralı) → T010-T015 (US3; T011 ‖ T012) → T016-T017
- T009 (SDK kök silme) T004-T007 kopyalarından SONRA gelmek zorunda
- T015 tüm onarımları bekler

## Parallel Example

```bash
# T004-T005 (Payment kopyaları) bittikten sonra:
Task: "T006 Merchant Provider + SubMerchants"
Task: "T007 Commission Provider + Payouts + TransactionReports"
# Onarımlarda: T011 ‖ T012 (farklı projeler)
```

## Implementation Strategy

MVP = US1 (CP.VPOS söküm). Sonra US2 dağıtım (kopyala → yeniden-namespace'le → SDK'yı sil
sırası KRİTİK), US3 derleme onarımı, Polish. Commit tek seferde (kullanıcı silmeleriyle
birlikte anlamlı bütün).