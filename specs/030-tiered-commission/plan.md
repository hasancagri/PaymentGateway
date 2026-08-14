# Implementation Plan: Tutar-Kademeli Komisyon Marjı

**Branch**: `030-tiered-commission` | **Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/030-tiered-commission/spec.md`

## Summary

Gateway marjı tek (oran, sabit) çiftinden tutar-kademeli tarifeye geçer: `MarginRule` VO'su yerini
sıralı `MarginTier` listesi taşıyan `MarginTariff` VO'suna bırakır (0'dan başlayan, kesin artan alt
sınırlar; son kademe açık uçlu; kademe başına mevcut tavanlar). Efektif komisyon hesabında tutarın
düştüğü TEK kademe tüm tutara uygulanır (bracket — dilimli değil); diğer korumalar aynen.
Create/UpdateMargin/List/Get sözleşmeleri `tiers` listesine döner; Admin CommissionPolicies ekranı
10 satırlık JS'siz kademe grid'i alır. Kararlar: [research.md](research.md) R1-R7.

## Technical Context

**Language/Version**: C# / .NET 10 (net10.0)

**Primary Dependencies**: Mevcut Commission.Api yığını — Marten (Postgres, Newtonsoft
NonPublicSetters), Wolverine, Minimal API, Razor Pages (Admin BFF). Yeni paket YOK.

**Storage**: commissionDb — `CommissionPolicy` dokümanında gömülü `Margin` obje→tablo şekil
değişimi; migration YOK, mevcut politika dokümanları sıfırlanır (R5, dev kuralı)

**Testing**: xUnit `tests/Commission.Api.Tests` — mevcut 20 test yeni imzaya taşınır + kademe
doğrulama/seçim/eşdeğerlik matrisi (R7); `tests/Merchant.Api.Tests` regresyon (dokunulmaz)

**Target Platform**: Aspire AppHost (macOS dev); Commission.Api http://localhost:5203, Admin 5204

**Project Type**: Mevcut BC içi model + sözleşme revizyonu + BFF ekran uyarlaması; yeni proje YOK

**Performance Goals**: Yok (≤10 kademelik listede lineer seçim)

**Constraints**: Bracket seçimi (tek kademe tüm tutara — FR-003); tam sınır üst kademeye;
tavanlar kademe başına (0.20 / 100); ≤10 kademe; `CalculateEffectiveCommission` dış sözleşmesi ve
`EffectiveCommission` VO'su DEĞİŞMEZ; tekil-aktif + statü makinesi DEĞİŞMEZ; JS'siz Admin formu

**Scale/Scope**: 1 VO değişimi (MarginRule→MarginTariff+MarginTier) + aggregate 3 metot dokunuşu +
4 slice sözleşme güncellemesi + Admin ekran/istemci revizyonu + test yeniden hizalama (~10 dosya)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Değerlendirme | Durum |
|---|---|---|
| I. BC İzolasyonu | Değişiklik Commission BC + Admin BFF içinde; cross-BC dokunuş yok. | ✅ |
| II. Zengin Domain | Tablo doğrulaması + kademe seçimi `MarginTariff` VO'sunda (private ctor + statik Create + helper — VO muafiyeti); aggregate davranışı korunur; anemik yapı yok. | ✅ |
| III. Vertical Slice + CQRS | Mevcut slice'lar yerinde revize; yeni slice yok; [Transactional]/IDocumentSession düzeni aynen. | ✅ |
| IV. Result Pattern | `MarginTariff.Create` → `ResultDomain<MarginTariff>`; kademe-işaretli `MessageItem.Property` (`Tiers[i].Alan`); mevcut sözleşme korunur. | ✅ |
| V. Kimlik + Açık Yetki | Uç policy'leri DEĞİŞMEZ (commission.read/write + AdminPlaneOnly / MerchantScoped). | ✅ |

**Gate sonucu**: GEÇTİ — ihlal yok (029'daki MerchantKey sapması bu spec'in kapsamına girmiyor).

## Project Structure

### Documentation (this feature)

```text
specs/030-tiered-commission/
├── plan.md              # Bu dosya
├── research.md          # R1-R7
├── data-model.md        # MarginTariff/MarginTier + CommissionPolicy değişimi + slice etkisi
├── quickstart.md        # S1-S4 canlı doğrulama (veri sıfırlama notu dahil)
├── contracts/
│   ├── api.md           # tiers gövdeli uç sözleşmeleri
│   └── admin-ui.md      # kademe grid'li ekran sözleşmesi
└── tasks.md             # /speckit-tasks üretecek
```

### Source Code (repository root)

```text
src/services/Commission.Api/Domains/CommissionPolicies/
├── ValueObjects/
│   ├── MarginTariff.cs                    # YENİ (MarginTier ile birlikte); MarginRule.cs SİLİNİR
│   └── EffectiveCommission.cs             # DEĞİŞMEZ
├── CommissionPolicy.cs                    # Margin tipi + Create/UpdateMargin imzaları + Calculate tek satır
└── Features/
    ├── Commands/{CreateCommissionPolicy,UpdateCommissionPolicyMargin}.cs   # Tiers sözleşmesi
    ├── Commands/ChangeCommissionPolicyStatus.cs                            # DEĞİŞMEZ
    └── Queries/{ListCommissionPolicies,GetCommissionPolicy}.cs             # Tiers yanıtı
        Queries/CalculateEffectiveCommission.cs                             # sözleşme DEĞİŞMEZ

src/ui/Admin/
├── Clients/CommissionPolicyApiClient.cs   # Tiers gövdeleri
├── Clients/ApiModels.cs                   # TierDto + model revizyonu
└── Pages/CommissionPolicies/Index.cshtml(.cs)  # kademe grid'i + kompakt tarife kolonu + Tarife Düzenle

tests/Commission.Api.Tests/                # imza taşıma + kademe matrisi (R7)
```

**Structure Decision**: Yerinde revizyon — dosya taşınmaz, `MarginRule.cs` silinip `MarginTariff.cs`
gelir; slice adları/rotaları sabit kalır (dış görünümde yalnız gövde şekli değişir).

## Complexity Tracking

> İhlal yok — tablo boş.