# Tasks: Payment Provider Domain Dağıtımı

**Feature**: 035-payment-provider-domain-distribution | **Branch**: `035-...` (034 üstüne)
**Input**: plan.md, spec.md, research.md, data-model.md, quickstart.md

Test görevi VAR: yeni VO `Create` doğrulaması saf domain birim testi (anayasa II — davranışlı domain
TDD; FR-008). Mevcut testler yeşil kalır.

Kritik (research R3): HTTP Input DTO (BuyerInput/BasketItemInput, TokenizeCard Pan/Expiry/Holder) KALIR;
VO handler'da araya girer (`Input → VO.Create → SDK wire`). "İki temsil": wire Buyer/Address/BasketItem/
CardInformation SDK'da DA kalır (handler map hedefi), domain VO'lar YENİ.

---

## Phase 1: Setup

- [x] T001 034 Iyzico.Provider ProjectReference'ının Payment.Api'de olduğunu doğrula (`src/services/Payment.Api/Payment.Api.csproj` — 034'te eklendi); yoksa ekle.

---

## Phase 2: User Story 1 — Provider/ klasörü kalkar, saf-wire SDK'ya (P1)

**Goal**: Payment.Api/Provider/ silinir; tüm iyzico wire tipleri Iyzico.Provider SDK'da.
**Independent Test**: `Provider/` dizini yok; `dotnet build` 0 hata; davranış korunur (yalnız relocation).

- [x] T002 [US1] Payment.Api/Provider/Payments (34 dosya) → `src/others/Iyzico.Provider/Payments/` taşı (`git mv`); namespace `Payment.Api.Provider.Payments` → `Iyzico.Provider.Payments`. (Buyer/Address/BasketItem wire DTO'ları DA taşınır — handler map hedefi; domain VO ayrı iş US2.)
- [x] T003 [US1] Payment.Api/Provider/Installments (6 dosya) → `src/others/Iyzico.Provider/Installments/`; namespace → `Iyzico.Provider.Installments`.
- [x] T004 [US1] Payment.Api/Provider/StoredCards (6 dosya) → `src/others/Iyzico.Provider/StoredCards/`; namespace → `Iyzico.Provider.StoredCards`. (CardInformation wire DTO DA taşınır.)
- [x] T005 [US1] `src/services/Payment.Api/Provider/` klasörünün tamamen boş/silinmiş olduğunu doğrula (`git rm` artıkları); klasör kalmaz.
- [x] T006 [US1] Payment GlobalUsings güncelle (`src/services/Payment.Api/GlobalUsings.cs`): `Payment.Api.Provider*` → `Iyzico.Provider*` (`.Payments/.Installments/.StoredCards` dahil).
- [x] T007 [US1] `dotnet build` — 0 hata (bu noktada davranış birebir aynı, yalnız wire relocation; VO henüz yok).

---

## Phase 3: User Story 2 — Domain-uygun tipler zengin VO olur (P1)

**Goal**: 4 domain VO (`Domains/.../ValueObjects/`); handler Input→VO→SDK wire zinciri.
**Independent Test**: 4 VO private ctor + `Create` ile var; VO doğrulama testleri yeşil; anemik record HTTP DTO'ya indirgendi.

### Testler (TDD — VO'dan önce)

- [x] T008 [P] [US2] Buyer VO testleri (`tests/Payment.Api.Tests/BuyerValueObjectTests.cs`): geçerli→`Ok`; geçersiz email/kimlik/boş alan→`Error`. Kırmızı başlar.
- [x] T009 [P] [US2] BasketItem VO testleri (`tests/Payment.Api.Tests/BasketItemValueObjectTests.cs`): geçerli→`Ok`; Price≤0/boş→`Error`.
- [x] T010 [P] [US2] CardInformation VO testleri (`tests/Payment.Api.Tests/CardInformationValueObjectTests.cs`): geçerli→`Ok`; Luhn/expiry geçersiz→`Error`.

### VO'lar (data-model.md)

- [x] T011 [P] [US2] `Domains/Payments/ValueObjects/Buyer.cs` — VO (private ctor + `static ResultDomain<Buyer> Create(...)` + email/kimlik/IP ince doğrulama). iyzico serileştirme YOK. `<summary>`+`<remarks>Handler: ChargePayment</remarks>`.
- [x] T012 [P] [US2] `Domains/Payments/ValueObjects/Address.cs` — VO; Buyer'dan türetme (`FromBuyer(Buyer)` veya `Create(contactName,city,country,description)`); shipping=billing (research R4).
- [x] T013 [P] [US2] `Domains/Payments/ValueObjects/BasketItem.cs` — VO (`Create(id,name,category1,price)`; Price>0).
- [x] T014 [P] [US2] `Domains/StoredCards/ValueObjects/CardInformation.cs` — VO (`Create(pan,expiry,holderName)`; Luhn+expiry; transient, kalıcı değil).

### Handler rewire (Input → VO → SDK wire)

- [x] T015 [US2] `Domains/Payments/Features/Commands/ChargePayment.cs`: handler `BuyerInput`→`Buyer.Create`, `BasketItemInput`→`BasketItem.Create`, `Address.FromBuyer`; VO'lardan SDK wire (`Iyzico.Provider.Payments.{Buyer,Address,BasketItem}`) kur (bugünkü `BuildRequest` alan-eşlemesi korunur). VO `Create` `Error` ise charge domain-sonucuyla reddedilir. BuyerInput/BasketItemInput record'ları HTTP DTO olarak KALIR.
- [x] T016 [US2] `Domains/StoredCards/Features/Commands/TokenizeCard.cs`: `Pan/Expiry/HolderName` → `CardInformation.Create` → SDK `CardInformation` wire → `Card.Create`. Geçersizse domain hata.
- [x] T017 [US2] `Domains/StoredCards/Features/Commands/RevokeCard.cs`: CardInformation kullanıyorsa VO'ya hizala; yoksa yalnız SDK using güncellemesi (Iyzico.Provider.StoredCards). Doğrula.

---

## Phase 4: User Story 3 — Çalışma-anı davranışı bit-korunur (P1)

**Goal**: davranış + kalıcı şema değişmez.
**Independent Test**: build 0 hata; tüm test yeşil; charge isteği bit-aynı; VO'da iyzico sızıntısı yok.

- [x] T018 [US3] `dotnet build` 0 hata + `dotnet test` — mevcut + yeni VO testleri %100 yeşil.
- [x] T019 [US3] Sızma kontrolü: `grep -rl "ToPKIRequestString\|CamelCase\|Newtonsoft" src/services/Payment.Api/Domains/*/ValueObjects/` → boş (VO iyzico bilmez, FR-005).
- [x] T020 [US3] Davranış-koruma: üretilen charge isteği (buyer/address/basket/card/tutar) dağıtımdan önceki alan-eşlemesiyle aynı (quickstart Adım 0↔5); Payment kalıcı şema değişmedi (VO aggregate alanı değil).

---

## Phase 5: Polish & Cross-Cutting

- [x] T021 [P] quickstart Adım 1–3 çalıştır: `Provider/` dizini yok, 4 VO var (private ctor+Create), anemik record domain-rolü kalmadı (SC-001/002/003).
- [x] T022 CLAUDE.md güncelle: Payment.Api girdisine "035: Provider/ kaldırıldı; saf-wire Iyzico.Provider SDK'ya, domain-uygun 4 tip Domains/ValueObjects VO'ya; handler anti-corruption sınırı" notu.
- [x] T023 Commit: `refactor(payment): 035 Provider/ kaldır — saf-wire SDK'ya, domain-uygun tipler VO'ya`.

---

## Dependencies

- Phase 1 → US1 (Phase 2) → US2 (Phase 3) → US3 (Phase 4) → Polish.
- US1 (taşıma) US2'den ÖNCE zorunlu: VO'lar SDK'daki wire tiplerine map'ler; Domains temiz olmalı.
- T002–T004 [P] işaretlenmedi (namespace + git mv sıralı, aynı SDK projesi build'i); T005/T006 sonra; T007 build kapısı.
- US2 testleri (T008–T010) VO impl'inden (T011–T014) ÖNCE (TDD, kırmızı→yeşil). T011–T014 [P] (farklı dosya). T015–T017 VO'lardan sonra (handler rewire).
- US3 (T018–T020) US2 bitince. T018→T019→T020.

## Parallel Opportunities

- **T008, T009, T010** (VO testleri, ayrı dosya) birlikte.
- **T011, T012, T013, T014** (VO impl, ayrı dosya) birlikte — ama testleri kırmızı olduktan sonra.
- **T021** doğrulama, build yeşilken.

## Implementation Strategy

**MVP = US1+US2+US3 birlikte** — tek atomik refactor (yarı-taşınmış Provider derlenmez). Sıra: SDK'ya
taşı (Provider/ sil) → VO'lar (TDD) + handler rewire → build/test yeşil + davranış-koruma → CLAUDE.md+commit.
US1 tek başına (yalnız taşıma) da derlenir/davranış-korur — ara güvenli nokta.

## Total

23 görev. US1: 6 (T002–T007), US2: 10 (T008–T017; 3 test + 4 VO + 3 handler), US3: 3 (T018–T020).
Setup 1, Polish 3.

---

## Ek (kullanıcı isteği — plan dışı, US1 ilkesinin devamı)

- [x] T024 `CardVault/` teknik-katman klasörünü söktü: `CardVault/PanTools.cs` (CardAssociationMapper + Bin/Last4/Brand türetici) → `Domains/StoredCards/PanTools.cs` (namespace `Payment.Api.Domains.StoredCards`); TokenizeCard using + CardBrand cref + test GlobalUsings güncellendi. Gerekçe: Provider/ ile aynı ilke — BC'de teknik-katman klasörü yok, her şey Domains'ten. (CardAssociationMapper SDK'ya taşınamaz — domain `CardBrand` döner, ters bağımlılık.)
