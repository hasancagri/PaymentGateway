# Data Model: Tutar-Kademeli Komisyon Marjı (030)

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## MarginTariff (YENİ VO — `ValueObjects/MarginTariff.cs`; `MarginRule` SİLİNİR)

Sıralı kademe listesi. Private ctor + statik `Create`; VO helper serbest (015 muafiyeti).

| Üye | Açıklama |
|---|---|
| `IReadOnlyList<MarginTier> Tiers` | FromAmount artan sıralı; en az 1, en çok `MaxTierCount` |
| `const int MaxTierCount = 10` | tablo üst sınırı |
| `const decimal MaxRatePercent = 0.20m` | kademe başına oran tavanı (MarginRule'dan taşınır) |
| `const decimal MaxFixedFee = 100m` | kademe başına sabit ücret tavanı |
| `static ResultDomain<MarginTariff> Create(IReadOnlyList<(decimal FromAmount, decimal RatePercent, decimal FixedFee)>)` | tüm FR-002 doğrulaması |
| `MarginTier ResolveTier(decimal paidPrice)` | `FromAmount <= paidPrice` olan SON kademe (tam sınır üst kademeye — R2) |

**Create doğrulama sırası** (ilk ihlalde `INVALID_VALUE` + sorunlu kademe indeksi `Property`'de,
ör. `Tiers[2].FromAmount`):
1. Liste boş → `VALUE_IS_REQUIRED`; eleman sayısı > 10 → `INVALID_VALUE`
2. `Tiers[0].FromAmount != 0` → hata
3. Her i>0 için `FromAmount[i] > FromAmount[i-1]` (kesin artan) değilse → hata
4. Her kademe: `0 ≤ RatePercent ≤ 0.20`, `0 ≤ FixedFee ≤ 100` değilse → hata

## MarginTier (YENİ iç VO — MarginTariff dosyasında veya `ValueObjects/MarginTier.cs`)

| Alan | Tip | Not |
|---|---|---|
| `FromAmount` | `decimal` | kademenin alt sınırı (TL, dahil); üst sınır = sonraki kademenin FromAmount'u (hariç) |
| `RatePercent` | `decimal` | kesir (0.02 = %2) |
| `FixedFee` | `decimal` | TL |

Doğrulama MarginTariff.Create'te (tablo bütünlüğü tek yerde); MarginTier saf taşıyıcı.

## CommissionPolicy (MEVCUT — değişir)

| Değişiklik | Detay |
|---|---|
| `Margin` alanı | `MarginRule` → `MarginTariff` |
| `Create(Guid merchantId, IReadOnlyList<(from,rate,fee)> tiers)` | imza değişir; boş-merchant reddi + `MarginTariff.Create` |
| `UpdateMargin(IReadOnlyList<(from,rate,fee)> tiers)` | imza değişir; hatada mevcut tarife DEĞİŞMEZ (FR-004) |
| `CalculateEffectiveCommission(...)` | gövdede tek satır değişir: `var tier = Margin.ResolveTier(paidPrice);` → `paidPrice * tier.RatePercent + tier.FixedFee` (R4); diğer korumalar aynen |
| `ChangeStatus` | DEĞİŞMEZ |

`EffectiveCommission` VO'su ve `CommissionPolicyStatus` DEĞİŞMEZ. Statü makinesi, tekil-aktif
kuralı (handler sorgusu), Marten kaydı aynen.

## Kalıcılık notu

`Margin` doküman içi gömülü obje → gömülü tablo olur; eski dokümanlar dönüştürülmez, commissionDb
politika dokümanları sıfırlanır (R5 — dev kuralı).

## Slice etkisi (plan referansı)

| Slice | Değişiklik |
|---|---|
| `CreateCommissionPolicy` | Command/Response `Tiers` listesi (R3) |
| `UpdateCommissionPolicyMargin` | Command/Response `Tiers` listesi |
| `ListCommissionPolicies` | Item'da `Tiers` listesi (RatePercent/FixedFee düz alanları kalkar) |
| `GetCommissionPolicy` | Yanıtta `Tiers` (merchant self-servis — FR-008) |
| `CalculateEffectiveCommission` | Sözleşme DEĞİŞMEZ (hesap içi kademe seçimi) |
| `ChangeCommissionPolicyStatus` | DEĞİŞMEZ |