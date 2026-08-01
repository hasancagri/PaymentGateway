# Tasks: Settlement Hesabı Yönetim Ekranları (Admin)

**Input**: Design documents from `/specs/005-settlement-account-admin-ui/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/screens.md, quickstart.md

**Tests**: Otomatik UI/entegrasyon testi YOK (proje deseni: yalnız saf domain birim testi; backend
zaten 004'te test edildi). Doğrulama quickstart.md 6 senaryosuyla elle yapılır.

**Organization**: Görevler kullanıcı hikâyesine göre gruplu; her hikâye bağımsız uygulanıp test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, tamamlanmamış göreve bağımlı değil)
- **[Story]**: US1 / US2 / US3
- Açıklamalarda tam dosya yolu var

## Path Conventions

Yalnız `src/ui/Admin/` değişir. **Backend (Merchant.Api/Commission.Api) HİÇ DEĞİŞMEZ** (FR-011).
Yeni proje/NuGet paketi yok (CPM korunur).

**Kod konvansiyonu (her kod görevine DAHİL — ayrı görev değil)**: Her yeni/değişen dosya (a) mevcut
Admin sayfa üslubunu izler (sunucu-render Razor, `BasePageModel`, `_Messages` partial, minimal JS),
(b) Türkçe yorum/XML doc, (c) kullanılmayan `using` bırakmaz. Bu maddeler her T00x kod görevinin parçası.

---

## Phase 1: Setup

**Purpose**: Yeni proje kurulumu yok. Baseline.

- [X] T001 `dotnet build src/ui/Admin/Admin.csproj` ile Admin BFF'nin temiz derlendiğini doğrula (değişiklik öncesi baseline)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: DTO'lar + typed client + DI kaydı. Bu faz bitmeden hiçbir ekran çalışamaz.

**⚠️ CRITICAL**: Bu faz bitmeden US1/US2/US3 başlayamaz.

- [X] T002 [P] Settlement DTO'larını ekle: `src/ui/Admin/Clients/ApiModels.cs` — `// ---- Merchant.Api (settlement) ----` bölümüne: `record CreateSettlementAccountRequest(string BankCode, string Iban, string AccountOwnerName, string AccountNo, string AccountDescription)`; `record UpdateSettlementAccountRequest(...)` (aynı alanlar); `record SetSettlementAccountStatusRequest(bool IsActive)`; `class SettlementAccountsResponse { List<SettlementAccountListItem> Accounts }`; `class SettlementAccountListItem { Guid Id; string BankCode; string? BankName; string Iban; string AccountOwnerName; string Status }`; `class SettlementAccountDetail { Guid Id; Guid MerchantId; string BankCode; string? BankName; string Iban; string AccountOwnerName; string AccountNo; string AccountDescription; string Status; DateTime CreatedTime }`; `class IdStatusResult { Guid Id; string Status }`. Mevcut record/class stilini izle. Bkz. data-model.md.
- [X] T003 `ISettlementAccountApiClient` + impl: `src/ui/Admin/Clients/SettlementAccountApiClient.cs` — `MerchantApiClient` desenini izle (`: ApiClientBase`). Metotlar (rota base `/api/v1/merchants/{merchantId}/settlement-accounts`): `GetAccountsAsync(Guid merchantId)` → `SettlementAccountsResponse`; `GetAccountAsync(Guid merchantId, Guid accountId)` → `SettlementAccountDetail`; `CreateAsync(Guid merchantId, CreateSettlementAccountRequest)` → `IdResult`; `UpdateAsync(Guid merchantId, Guid accountId, UpdateSettlementAccountRequest)` → `IdResult`; `SetStatusAsync(Guid merchantId, Guid accountId, SetSettlementAccountStatusRequest)` → `IdStatusResult`. Hepsi `SendAsync<T>` ile. (T002'ye bağlı.)
- [X] T004 DI kaydı: `src/ui/Admin/Program.cs` — mevcut `AddHttpClient<IMerchantApiClient,...>` bloğunun yanına `builder.Services.AddHttpClient<ISettlementAccountApiClient, SettlementAccountApiClient>(client => client.BaseAddress = new Uri("http://merchant-api"));` ekle. (T003'e bağlı.)

**Checkpoint**: `dotnet build` yeşil (client + DTO derlenir; henüz ekran yok).

---

## Phase 3: User Story 1 — Hesapları gör (Priority: P1) 🎯 MVP

**Goal**: Gateway admin bir merchant seçer, o merchant'ın settlement hesaplarını tabloda görür (banka
kod+ad, IBAN, sahip, durum). Yalnız o merchant; boş liste bilgilendirici.

**Independent Test**: 004 API'siyle bir merchant'a iki hesap ekle; Admin'de merchant'ı seç → iki satır
doğru alanlarla; başka merchant → onlar görünmez / boş.

- [X] T005 [US1] Index sayfası: `src/ui/Admin/Pages/SettlementAccounts/Index.cshtml.cs` — `IndexModel : BasePageModel`, ctor `IMerchantApiClient` + `ISettlementAccountApiClient`; `[BindProperty(SupportsGet = true)] Guid? MerchantId`; `List<MerchantListItem> Merchants`; `List<SettlementAccountListItem> Accounts`. `OnGetAsync`: merchant'ları yükle (`GetAllAsync`); `MerchantId` doluysa `GetAccountsAsync` → `Accounts`; hata → `AddErrors`. `MerchantCommissions/Index.cshtml.cs` desenini izle.
- [X] T006 [US1] Index görünümü: `src/ui/Admin/Pages/SettlementAccounts/Index.cshtml` — `@page`, `<partial name="_Messages" />`; merchant `<select onchange="this.form.submit()">` (MerchantCommissions/Index deseni); merchant seçilmemişse "hesap görmek için merchant seç" (muted); seçili + boşsa "hesap yok + Yeni ekle" bağlantısı; doluysa tablo: Banka (`@bankCode (@bankName)`), IBAN, Sahip, Durum (Active/Passive), aksiyon "Düzenle" → `Edit?merchantId&accountId`. Üstte "Yeni hesap" → `Create?merchantId` (yalnız seçiliyken). (T005 ile birlikte.)
- [X] T007 [US1] Navigasyon bağlantısı: `src/ui/Admin/Pages/Merchants/Details.cshtml` — aksiyon barına, "Komisyonları" butonunun yanına `<a class="btn" asp-page="/SettlementAccounts/Index" asp-route-merchantId="@m.Id">Settlement Hesapları</a>` ekle.

**Checkpoint**: US1 tek başına test edilebilir — hesaplar görülür, tenant izolasyonu + boş durum çalışır. **MVP burada.**

---

## Phase 4: User Story 2 — Hesap ekle (Priority: P2)

**Goal**: Admin seçili merchant'a yeni hesap ekler (banka dropdown + IBAN + sahip + hesap no + açıklama).
Geçersiz giriş anlaşılır hata; form korunur.

**Independent Test**: Geçerli banka + TR IBAN + sahiple gönder → hesap listede; bozuk IBAN → hata, eklenmez, girdiler durur; mükerrer IBAN → hata.

- [X] T008 [US2] Create sayfası: `src/ui/Admin/Pages/SettlementAccounts/Create.cshtml.cs` — `CreateModel : BasePageModel`, ctor `ISettlementAccountApiClient` + `ICommissionApiClient`; `[BindProperty(SupportsGet=true)] Guid MerchantId`; `[BindProperty] Input` (`BankCode, Iban, AccountOwnerName, AccountNo, AccountDescription`); `List<BankCatalogItem> Banks`. `OnGetAsync`: banka katalogunu yükle (`GetBankCatalogAsync(onlyAvailable:false)`). `OnPostAsync`: `CreateAsync(MerchantId, req)`; başarı → `Flash="Hesap eklendi."` + `RedirectToPage("Index", new { merchantId = MerchantId })`; hata → `AddErrors` + katalog yeniden yükle + `return Page()`. `Banks/Create.cshtml.cs` desenini izle. (research D5: UI ek doğrulama yok.)
- [X] T009 [US2] Create görünümü: `src/ui/Admin/Pages/SettlementAccounts/Create.cshtml` — `@page`, `_Messages`; form `method="post"`; gizli/rota `merchantId`; **Banka** `<select asp-for="Input.BankCode">` katalogdan (`value=Code`, metin `@c.Code — @c.Name`); IBAN, Hesap Sahibi, Hesap No (opsiyonel), Açıklama (opsiyonel) text alanları; "Kaydet" + "İptal" (Index'e). Banka serbest giriş yok (FR-006). (T008 ile birlikte.)

**Checkpoint**: US1 + US2 bağımsız çalışır; ekleme + doğrulama reddi + tenant korunur.

---

## Phase 5: User Story 3 — Düzenle ve aktif/pasif (Priority: P3)

**Goal**: Admin var olan hesabı günceller ve aktif/pasif yapar (silmez). Geçersiz güncelleme reddedilir,
eski değer korunur.

**Independent Test**: Geçerli yeni IBAN+sahiple güncelle → liste yeni değer; pasife al → satır "Passive" ama durur; bozuk IBAN → hata, eski değer korunur.

- [X] T010 [US3] Edit sayfası: `src/ui/Admin/Pages/SettlementAccounts/Edit.cshtml.cs` — `EditModel : BasePageModel`, ctor `ISettlementAccountApiClient` + `ICommissionApiClient`; `[BindProperty(SupportsGet=true)] Guid MerchantId, Guid AccountId`; `[BindProperty] Input` (Create ile aynı); `List<BankCatalogItem> Banks`; `string Status`. `OnGetAsync`: `GetAccountAsync(MerchantId, AccountId)` — null → `NotFound` bilgisi (tenant sızıntısı yok); doluysa `Input`+`Status` doldur, katalog yükle. `OnPostAsync` (Kaydet): `UpdateAsync(MerchantId, AccountId, req)`; başarı → Flash + Index redirect; hata → `AddErrors` + katalog + `return Page()`. `OnPostToggleStatusAsync`: `SetStatusAsync(MerchantId, AccountId, new(!isActive))`; sonuç `Status` ile geri. `Banks/Edit.cshtml.cs` desenini izle.
- [X] T011 [US3] Edit görünümü: `src/ui/Admin/Pages/SettlementAccounts/Edit.cshtml` — `@page`, `_Messages`; hesap yoksa "bulunamadı" (muted); varsa dolu form (Create ile aynı alanlar, banka dropdown seçili); "Kaydet"; ayrı **aktif/pasif** aksiyonu (`asp-page-handler="ToggleStatus"`) — mevcut duruma göre "Pasife al"/"Aktif et"; durum rozeti. Silme butonu YOK. (T010 ile birlikte.)

**Checkpoint**: Tüm hikâyeler tamam; liste + ekle + düzenle + durum uçtan uca.

---

## Phase 6: Polish & Cross-Cutting

- [X] T012 `dotnet build` (çözüm) yeşil; yalnız `src/ui/Admin` diff'i olduğunu doğrula (backend değişmedi — FR-011)
- [X] T013 Aspire ile ayağa kaldırıp quickstart.md 6 senaryosunu elle doğrula (`dotnet run --project src/aspire/AppHost/AppHost.csproj`, admin-web); özellikle tenant izolasyonu (cross-merchant liste boş / Edit 404), banka yalnız dropdown, hata Türkçe + form korunur, pasif kayıt durur

> **Not**: `MessageText` gerekli kodları (VALUE_IS_REQUIRED/INVALID_FORMAT/RECORD_NOT_FOUND/RECORD_DUPLICATE/
> SERVER_ERROR) zaten içeriyor → ek görev yok. Türkçe yorum + temiz using her kod görevine gömülü (T002–T011).

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002–T004)** → hikâyeler.
- Foundational içi: T002 [P] → T003 (client, T002'ye bağlı) → T004 (DI, T003'e bağlı).
- **US1 (T005–T007)**: Foundational sonrası. T005+T006 aynı sayfa (sıralı/birlikte); T007 ayrı dosya [P].
- **US2 (T008–T009)**: Foundational sonrası; US1'den bağımsız (ayrı sayfa). Create linki US1 Index'ten gelir ama sayfa bağımsız derlenir.
- **US3 (T010–T011)**: Foundational sonrası; US1/US2'den bağımsız (ayrı sayfa).
- **Polish (T012–T013)**: tüm hikâyeler sonrası.

**Not**: Üç ekran ayrı klasör/dosya → US'ler Foundational sonrası paralel alınabilir. Tek ortak dosya
`Program.cs` (T004) ve `ApiModels.cs` (T002) foundational'da bitirilir; sonra çakışma yok.

## Parallel Opportunities

- Foundational başlangıcı: **T002** tek başına (T003/T004 ona zincirli).
- Hikâyeler: Foundational sonrası **US1 + US2 + US3** paralel (ayrı sayfa dosyaları).
- US1 içinde **T007** (Details butonu) backend'den bağımsız [P].

## Implementation Strategy

- **MVP = Phase 1 + 2 + 3 (US1).** Görünürlük — admin hesapları API'siz görür. Teslim edilebilir.
- Sonra US2 (ekle) → US3 (düzenle/durum) artımlı.
- Her checkpoint'te `dotnet build` yeşil; backend'e dokunulmadığı doğrulanır.

## Task Summary

- **Toplam**: 13 görev (T001–T013)
- **Setup**: 1 (T001) · **Foundational**: 3 (T002–T004)
- **US1 (P1/MVP)**: 3 (T005–T007) · **US2 (P2)**: 2 (T008–T009) · **US3 (P3)**: 2 (T010–T011)
- **Polish**: 2 (T012–T013)
- **Paralel [P]**: T002, T007
- **Otomatik test**: yok (UI; quickstart elle). **Backend değişikliği**: yok (FR-011).