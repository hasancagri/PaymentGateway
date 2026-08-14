# Tasks: Tutar-Kademeli Komisyon Marjı

**Input**: Design documents from `/specs/030-tiered-commission/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R7), data-model.md, contracts/, quickstart.md

**Tests**: Saf domain birim testleri DAHİL (R7 — 023/024 deseni); handler/HTTP entegrasyonu
quickstart ile elle.

**Organization**: User story bazlı. NOT: aggregate imza değişimi tüm slice'ları kırar — Foundational
faz VO+aggregate+test taşımasını birlikte bitirip build'i yeşile döndürür; story fazları sözleşme/UI
katmanını taşır.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Taban çizgisi: `dotnet build` 0 hata + `dotnet test tests/Commission.Api.Tests` 20/20
      yeşil (değişiklik öncesi referans; sapma varsa önce onu çöz).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: VO + aggregate + test taşıması — tek build kapısı; tüm story'ler buna bağlı.

- [X] T002 `MarginTariff` + `MarginTier` VO'ları (data-model.md): `Create(IReadOnlyList<(from,rate,
      fee)>)` doğrulama sırası (boş→VALUE_IS_REQUIRED; >10, ilk!=0, artmayan, tavan aşımı→
      INVALID_VALUE; `Property = "Tiers[i].<Alan>"` kademe işaretli) + `ResolveTier(paidPrice)`
      (FromAmount <= paidPrice olan SON kademe); sabitler MaxTierCount=10, MaxRatePercent=0.20m,
      MaxFixedFee=100m — `src/services/Commission.Api/Domains/CommissionPolicies/ValueObjects/MarginTariff.cs`
- [X] T003 `CommissionPolicy` revizyonu: `Margin` alanı `MarginTariff`; `Create(Guid,
      IReadOnlyList<(from,rate,fee)>)` + `UpdateMargin(IReadOnlyList<...>)` (hatada mevcut tarife
      değişmez) + `CalculateEffectiveCommission` marj satırı `Margin.ResolveTier(paidPrice)` ile
      (diğer korumalar/yuvarlama AYNEN — R4); `<remarks>Handler:</remarks>` notları güncel;
      `ValueObjects/MarginRule.cs` SİLİNİR —
      `src/services/Commission.Api/Domains/CommissionPolicies/CommissionPolicy.cs`
- [X] T004 Test taşıma + kademe matrisi: mevcut 20 test yeni imzaya (tek kademeli tabloyla) taşınır;
      yeni testler — tablo doğrulama matrisi (boş, >10, ilk!=0, artmayan, kademe-başına tavanlar,
      kademe indeksli Property), `ResolveTier` seçimi (iç/tam sınır üst kademeye/açık uç), hatalı
      UpdateMargin'de tarife değişmedi — `tests/Commission.Api.Tests/` (mevcut dosya + gerekirse
      `MarginTariffTests.cs`)
- [X] T005 Checkpoint: slice'lar HENÜZ eski sözleşmede derlenmeyeceği için bu noktada yalnız domain
      derlemesi hedeflenir — T006-T008'i tamamlamadan build yeşile dönmez; T004 testleri T008
      sonrası koşulur (sıra notu).

---

## Phase 3: User Story 1 — Kademeli Tarife Tanımlama (P1) 🎯 MVP

**Goal**: Ekrandan/API'den kademe tablosu girilir, doğrulanır, listede görünür.

**Independent Test**: quickstart S1 — üç kademeli oluşturma + 4 negatif.

- [X] T006 [US1] `CreateCommissionPolicy` sözleşmesi: `TierDto(decimal FromAmount, decimal
      RatePercent, decimal FixedFee)` + Command/Response `Tiers` listesi (contracts/api.md);
      handler tekil-aktif sorgusu AYNEN —
      `src/services/Commission.Api/Domains/CommissionPolicies/Features/Commands/CreateCommissionPolicy.cs`
- [X] T007 [P] [US1] `ListCommissionPolicies` yanıtı: `RatePercent/FixedFee` düz alanları yerine
      `Tiers` listesi —
      `src/services/Commission.Api/Domains/CommissionPolicies/Features/Queries/ListCommissionPolicies.cs`
- [X] T008 [P] [US1] `GetCommissionPolicy` (MerchantScoped self) + `UpdateCommissionPolicyMargin`
      yanıt/gövde şekilleri `Tiers`'a döner (FR-008; Update'in UI'ı US3'te ama sözleşme build için
      burada taşınır) —
      `src/services/Commission.Api/Domains/CommissionPolicies/Features/Queries/GetCommissionPolicy.cs`,
      `src/services/Commission.Api/Domains/CommissionPolicies/Features/Commands/UpdateCommissionPolicyMargin.cs`
- [X] T009 [US1] Build kapısı: `dotnet build` 0 hata + `dotnet test tests/Commission.Api.Tests`
      yeşil (T004 matrisi dahil).
- [X] T010 [US1] Admin istemci/model: `TierDto` + `CommissionPolicyItem.Tiers` +
      `CreateCommissionPolicyRequest(MerchantId, List<TierDto>)` + `UpdateMarginAsync(merchantId,
      List<TierDto>)` — `src/ui/Admin/Clients/ApiModels.cs`, `src/ui/Admin/Clients/CommissionPolicyApiClient.cs`
- [X] T011 [US1] Admin ekran — oluşturma + liste (contracts/admin-ui.md): 10 satırlık indeksli
      kademe grid'i (`Tiers[i].FromAmount/RatePercent/FixedFee`, boş satır atlama), liste
      `Tarife` kolonu kompakt gösterim (`0+: %2,5 + 1 TL · ...`) —
      `src/ui/Admin/Pages/CommissionPolicies/Index.cshtml(.cs)`
- [X] T012 [US1] Checkpoint: Admin build 0 hata; quickstart S1 (üç kademe + 4 negatif) elle geçer
      (veri sıfırlama ön koşuluyla — quickstart not).

---

## Phase 4: User Story 2 — Tutara Göre Doğru Kademeden Hesap (P1)

**Goal**: Hesap, tutarın düştüğü tek kademenin oran+sabitiyle yapılır (bracket).

**Independent Test**: quickstart S2 vektörleri + S4 eşdeğerlik.

- [X] T013 [P] [US2] Hesap vektör testleri: spec tarifesiyle 500→13.50, 1000→21.00 (tam sınır üst
      kademe), 20000→360.00 marj; tek-kademe tablo = düz model birebir (SC-004; 100 TL → 3.00);
      efektif>tutar koruması kademeli örnekle — `tests/Commission.Api.Tests/` (T004 dosyalarına ek)
- [X] T014 [US2] Checkpoint: testler yeşil; quickstart S2 canlı (`/effective-commission` üç tutar +
      koruma senaryosu) elle geçer.

---

## Phase 5: User Story 3 — Tarife Güncelleme (P2)

**Goal**: Tablo bütünüyle değiştirilir; tarihçe pasifleştir+yeni-oluştur yoluyla.

**Independent Test**: quickstart S3.

- [X] T015 [US3] Admin "Tarife Düzenle": listede satır linki → aynı sayfa `merchantId` query
      param'ıyla grid'i mevcut kademelerle doldurur, kaydet → `UpdateMarginAsync`
      (contracts/admin-ui.md) — `src/ui/Admin/Pages/CommissionPolicies/Index.cshtml(.cs)`
- [X] T016 [US3] Checkpoint: quickstart S3 elle (güncelle → yeni orandan hesap; bozuk tablo →
      eski tarife durur; pasifleştir + yeni oluştur → eski kayıt kademeleriyle görünür).

---

## Phase 6: Polish & Kapanış

- [X] T017 Tam kapanış: `dotnet build` (çözüm) 0 hata; `Commission.Api.Tests` + `Merchant.Api.Tests`
      yeşil; quickstart S1-S4 tamam; commissionDb eski politika dokümanlarının sıfırlandığı not
      düşülür (R5). Commit/PR kullanıcı onayıyla.

---

## Dependencies

```
T001 ─► Phase 2 (T002 → T003; T004 T003'ten sonra yazılır, T009'da koşulur)
Phase 2 ─► US1 (T006 → T007 ∥ T008 → T009 → T010 → T011 → T012)
T009 ─► US2 (T013 → T014)          # hesap domain'de hazır; test+canlı doğrulama
US1 ─► US3 (T015 → T016)           # Düzenle UI'ı create grid'ini yeniden kullanır
US1+US2+US3 ─► T017
```

**Story sırası**: US1 → US2 → US3. NOT: T005 gerçek bir kapı değil, sıra uyarısıdır — Foundational
+ US1 sözleşme taşıması (T002-T009) tek yeşil-build dilimi olarak koşulur.

## Parallel Opportunities

- T007 ∥ T008 (ayrı dosyalar, T006'daki TierDto'ya bağlı — TierDto ortak tipi T006'da doğar)
- T013, T010 ile paralel yazılabilir (test ↔ UI ayrı dosyalar)

## Implementation Strategy

**MVP**: T001-T012 (Foundational + US1) — kademeli tarife girilip listelenebilir; hesap zaten
domain'de kademeli (T003). US2 doğrulamayı sayısal vektörlerle mühürler, US3 işletim konforu.
Aggregate imzası değiştiği için Foundational+US1 sözleşme taşıması bölünmez tek dilimdir; build
ancak T009'da yeşile döner — ara commit atılmaz.
> Canlı doğrulama (2026-08-14): S1-S3 GEÇTİ (üç kademeli oluşturma + Tiers[0] doğrulama reddi canlıda; hesap vektörleri 500→13,50 / 1000→21,00 / 5000→60,00 / 20000→240,00 + efektif>tutar koruması; tarife güncelleme UI+API + statü geçişleri). S4 tek-kademe eşdeğerliği birim testle mühürlü (SC-004). commissionDb eski politika dokümanları truncate edildi (R5). Not: ilk UI güncelleme denemesi AppHost restart penceresine denk gelip boşa düştü — tekrar denemede geçti.
