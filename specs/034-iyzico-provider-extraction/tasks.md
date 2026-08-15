# Tasks: Iyzico.Provider Çekirdek Çıkarımı

**Feature**: 034-iyzico-provider-extraction | **Branch**: `034-iyzico-provider-extraction`
**Input**: plan.md, spec.md, research.md, quickstart.md

Test-yazma görevi YOK: anayasa saf-domain birim testi ister; bu refactor yeni domain getirmez.
Doğrulama = mevcut testlerin yeşil kalması + davranış-koruma (build/test/md5/istek karşılaştırma).

Kaynak-doğruluk: 14 çekirdek dosya üç BC'de md5-özdeş → **Payment.Api/Provider** kopyası taşınır
(git mv), Merchant + Commission kopyaları silinir.

---

## Phase 1: Setup

- [x] T001 Yeni class lib projesi oluştur: `src/others/Iyzico.Provider/Iyzico.Provider.csproj` (TargetFramework `net10.0`, `Nullable` + `ImplicitUsings` açık, tek `<PackageReference Include="Newtonsoft.Json" />` — SÜRÜMSÜZ, CPM). Referans: `src/others/Common/Common.csproj` stili.
- [x] T002 Çözüme ekle: `PaymentGateway.slnx`'e `<Project Path="src/others/Iyzico.Provider/Iyzico.Provider.csproj" />` satırını `src/others/` blokuna (Common/Shared yanına) ekle.

---

## Phase 2: Foundational (tüm story'leri bloklar)

14 çekirdek dosyayı taşı + namespace + görünürlük. Bu tamamlanmadan hiçbir BC derlenmez.

- [x] T003 14 çekirdek dosyayı Payment.Api'den yeni projeye taşı (`git mv`): `src/services/Payment.Api/Provider/{BaseRequestV2,DigestHelper,HashGeneratorV2,HttpClient,JsonBuilder,PagingRequest,ProviderConstants,ProviderOptions,ProviderResourceV2,RequestFormatter,RequestStringConvertible,RestHttpClientV2,StringHelper,ToStringRequestBuilder}.cs` → `src/others/Iyzico.Provider/`.
- [x] T004 Merchant.Api ve Commission.Api'deki aynı 14 çekirdek dosya kopyalarını sil (`git rm`): `src/services/Merchant.Api/Provider/<14>.cs` + `src/services/Commission.Api/Provider/<14>.cs` (alt klasörler Onboarding/Payout/Reporting DOKUNULMAZ).
- [x] T005 Taşınan 14 dosyada namespace'i `Iyzico.Provider` yap (`namespace Payment.Api.Provider` → `namespace Iyzico.Provider`) — `src/others/Iyzico.Provider/*.cs`.
- [x] T006 Görünürlük: `src/others/Iyzico.Provider/RestHttpClientV2.cs` — `class RestHttpClientV2` → `public class RestHttpClientV2` (alt klasörler ayrı assembly'den `.Create()` çağırır). `StringHelper.cs` `internal` KALIR (dokunma). Diğerleri zaten public.

---

## Phase 3: User Story 1 — Tek-kaynak transport çekirdeği (P1)

**Goal**: her BC paylaşılan çekirdeğe referans verir, kendi kopyası kalmaz.
**Independent Test**: `dotnet build` 0 hata; `ls <BC>/Provider/*.cs | grep <çekirdek>` her BC'de 0.

- [x] T007 [P] [US1] Payment.Api ProjectReference: `src/services/Payment.Api/Payment.Api.csproj`'a `<ProjectReference Include="..\..\others\Iyzico.Provider\Iyzico.Provider.csproj" />` ekle.
- [x] T008 [P] [US1] Merchant.Api ProjectReference: `src/services/Merchant.Api/Merchant.Api.csproj`'a aynı ProjectReference'ı ekle.
- [x] T009 [P] [US1] Commission.Api ProjectReference: `src/services/Commission.Api/Commission.Api.csproj`'a aynı ProjectReference'ı ekle.
- [x] T010 [US1] Payment GlobalUsings: `src/services/Payment.Api/GlobalUsings.cs` — `global using Payment.Api.Provider;` → `global using Iyzico.Provider;` (alt-namespace satırları `.StoredCards/.Payments/.Installments` KALIR).
- [x] T011 [US1] Commission GlobalUsings: `src/services/Commission.Api/GlobalUsings.cs` — `global using Commission.Api.Provider;` → `global using Iyzico.Provider;` (`.Payout/.Reporting` KALIR).
- [x] T012 [US1] Merchant GlobalUsings: `src/services/Merchant.Api/GlobalUsings.cs` — `global using Iyzico.Provider;` EKLE (bugün Provider using'i YOK; Onboarding namespace-nesting'e güveniyordu — en yüksek regresyon riski).
- [x] T013 [US1] Payment ProviderOptions map tip-adı: `src/services/Payment.Api/Program.cs` — `new Payment.Api.Provider.ProviderOptions { ... }` → `new Iyzico.Provider.ProviderOptions { ... }` (`AddSingleton<Payment.Api.Provider.ProviderOptions>` da `Iyzico.Provider.ProviderOptions`). `IyzicoProviderSettings` (Options POCO, secret) BC'de KALIR, DOKUNMA.

---

## Phase 4: User Story 2 — Sınır kuralının korunması (P1)

**Goal**: paylaşılan projede yalnız transport; BC-özel tip/secret sızmaz.
**Independent Test**: grep ile Iyzico.Provider'da BC-özel tip 0; cross-BC erişim derleme ile kapalı.

- [x] T014 [US2] Doğrula: `src/others/Iyzico.Provider/` içinde BC-özel istek/yanıt tipi veya secret'lı config YOK — `grep -rlE "SubMerchant|Payout|CrossBooking|TransactionReport|CreatePaymentRequest|IyzicoProviderSettings" src/others/Iyzico.Provider/*.cs` → boş (0). Varsa dosya yanlış taşınmış, geri al.
- [x] T015 [US2] Doğrula: BC-özel alt klasörler yerinde ve namespace'leri korunmuş — `src/services/Payment.Api/Provider/{Payments,Installments,StoredCards}`, `Merchant.Api/Provider/Onboarding`, `Commission.Api/Provider/{Payout,Reporting}` var; içleri `namespace <BC>.Provider.<Sub>` (değişmedi).

---

## Phase 5: User Story 3 — Çalışma-anı davranışı değişmez (P1)

**Goal**: davranış bit-düzeyinde korunur.
**Independent Test**: build 0 hata; `dotnet test` yeşil; (ops.) canlı charge önceki gibi.

- [x] T016 [US3] `dotnet build` — 0 hata. Hata çıkarsa öncelik sırası: (a) Merchant Onboarding using (T012), (b) RestHttpClientV2 public (T006), (c) Payment Program.cs map (T013).
- [x] T017 [US3] `dotnet test` — mevcut testler taşımadan önceki gibi %100 geçer.
- [~] T018 (ATLANDI — kullanıcı kararı; 032/033 charge canlı geçti, transport değişmiyor) [US3] [P] (opsiyonel canlı) Aspire ile sandbox charge smoke — `dotnet run --project src/aspire/AppHost/AppHost.csproj`, test kartıyla (`reference_iyzico_sandbox_test_cards`: 540667 İş Maximum taksitli) charge → iyzico işlem no döner, imza/hash reddi YOK. Bkz. quickstart Adım 5.

---

## Phase 6: Polish & Cross-Cutting

- [x] T019 [P] quickstart Adım 3–4 çalıştır: BC'lerde çekirdek kalıntı 0, Iyzico.Provider'da 14 dosya (SC-001), BC-özel tip sızıntısı 0 (SC-004).
- [x] T020 CLAUDE.md güncelle: Payment/Merchant/Commission `Provider/` açıklamalarına "çekirdek transport `src/others/Iyzico.Provider`'a çıkarıldı (034); BC yalnız istek/yanıt alt klasörlerini + secret'lı settings'i tutar" notu.
- [x] T021 Commit: `refactor(provider): 034 iyzico transport çekirdeğini Iyzico.Provider'a çıkar` (staged 032/033 artığı Common/Commission edit'lerini ayrı değerlendir).

---

## Dependencies

- Phase 1 (T001–T002) → Phase 2 (T003–T006) → Phase 3+ .
- T003 (mv) T004'ten (rm) önce mantıksal; ikisi de T005/T006'dan önce.
- T007–T009 [P] birbirinden bağımsız (farklı csproj). T010–T013 GlobalUsings/Program (farklı dosya, [P] değil işaretlenmedi çünkü aynı BC build'ini etkiler — sıra serbest ama build T016'da).
- US1 (Phase 3) tamamlanmadan US3 build (T016) geçmez. US2 (Phase 4) doğrulama-only, T016 sonrası da koşulabilir.
- T016 → T017 → T018.

## Parallel Opportunities

- **T007, T008, T009** birlikte (üç ayrı csproj ProjectReference).
- **T018, T019** doğrulama, build yeşilken paralel.

## Implementation Strategy

**MVP = US1 + US2 + US3 birlikte** — bu tek atomik refactor; parçalı teslim yok (yarı-taşınmış
çekirdek derlenmez). Sıra: Setup (proje+slnx) → çekirdeği taşı/görünürlük → BC wiring (ref+using+map)
→ build/test yeşil → doğrulama → CLAUDE.md + commit.

## Total

21 görev. US1: 7 (T007–T013), US2: 2 (T014–T015), US3: 3 (T016–T018). Setup 2, Foundational 4, Polish 3.
