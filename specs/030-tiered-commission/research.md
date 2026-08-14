# Research: Tutar-Kademeli Komisyon Marjı (030)

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

## R1 — VO dönüşümü: `MarginRule` → `MarginTariff` (+ iç `MarginTier`)

**Decision**: `MarginRule` (tek oran+sabit) yerini `MarginTariff` VO'suna bırakır: sıralı
`MarginTier` listesi taşır; `MarginTier` = (FromAmount, RatePercent, FixedFee) iç VO'su.
`MarginTariff.Create(IReadOnlyList<(from,rate,fee)>)` tüm tablo doğrulamasını yapar (FR-002);
`ResolveTier(paidPrice)` kademe seçer (VO helper serbest — 015 muafiyeti). `MarginRule` SİLİNİR
(dev aşaması, iki model yaşatılmaz; tek kademeli tablo aynı işi görür).

**Rationale**: Doğrulama + seçim mantığı tek VO'da toplanır; aggregate yalnız `Margin.ResolveTier`
çağırır. Anayasa II: kimliksiz kavram VO.

**Alternatives considered**: MarginRule'u koruyup yanına liste eklemek — reddedildi (iki gerçek
kaynağı, ölü alan); ayrı Tier aggregate'i — reddedildi (kimliği yok, tablo bütün olarak doğrulanır).

## R2 — Kademe temsili: yalnız alt sınır (FromAmount), son kademe açık uçlu

**Decision**: Kademe yalnız alt sınırını taşır; üst sınır = sonraki kademenin alt sınırı.
Doğrulama: liste boş değil (≤10 kademe), ilk `FromAmount == 0`, alt sınırlar kesin artan,
her kademe oran/sabit tavan içinde (0.20 / 100). Seçim: `FromAmount <= paidPrice` olan SON kademe
(tam sınır üst kademeye düşer — spec US2/AC3 bununla otomatik sağlanır).

**Rationale**: Boşluk/bindirme yapısal olarak imkânsız (FR-002'nin "yapısal" şartı); tek alanla
temsil, form ve doğrulama basit.

## R3 — Sözleşme değişimi: create/update/get/list gövdeleri `tiers` listesi taşır

**Decision**: `CreateCommissionPolicyCommand(Guid MerchantId, List<TierDto> Tiers)` ve
`UpdateCommissionPolicyMarginCommand(Guid MerchantId, List<TierDto> Tiers)`;
`TierDto(decimal FromAmount, decimal RatePercent, decimal FixedFee)`. List/Get/Create yanıtları
düz RatePercent/FixedFee yerine `Tiers` listesi döner. Geriye uyum YOK — eski istemci yalnız bu
oturumda yazılan Admin ekranı; birlikte güncellenir.

**Rationale**: Dev aşaması, dış tüketici yok; çift-format taşımak gereksiz.

## R4 — Hesap değişimi minimal: yalnız marj satırı kademeden

**Decision**: `CalculateEffectiveCommission` akışı aynı kalır (statü/tutar/iyzico-parse/
efektif>tutar korumaları AYNEN); tek fark `Margin.RatePercent/FixedFee` yerine
`var tier = Margin.ResolveTier(paidPrice)` sonrası `paidPrice * tier.RatePercent + tier.FixedFee`.
Yuvarlama aynı (2 ondalık AwayFromZero, marj satırında). `EffectiveCommission` VO değişmez.

**Rationale**: SC-004 (tek kademe = eski davranış birebir) ancak böyle garanti edilir.

## R5 — Veri: dönüştürme yok, commissionDb politika dokümanları sıfırlanır

**Decision**: Eski `Margin` şekli (obje) yeni şekle (tablo) migrate EDİLMEZ; dev kuralı
(defansif migration yok) gereği mevcut CommissionPolicy dokümanları silinir/DB sıfırlanır,
tarifeler ekrandan yeniden girilir. Quickstart'a not düşülür.

**Rationale**: feedback_dev_phase_no_defensive_migrations; işlem verisi yok, kayıp yok.

## R6 — Admin formu: sabit 10 satırlık kademe grid'i (JS yok)

**Decision**: Oluşturma/güncelleme formu `Tiers[i].FromAmount/RatePercent/FixedFee` adlı
indeksli input'larla en fazla 10 satır render eder (mevcut kademeler dolu, kalanlar boş);
tamamen boş satırlar post'ta atlanır. JS'siz düz Razor form (Admin'de script yok — mevcut stil).
Listede kademeler kompakt gösterilir: `0+: %2,5+1 · 1.000+: %2+1 · 10.000+: %1,8`.

**Rationale**: BFF kural sızdırmaz, satır ekle/çıkar davranışı için JS yatırımı gereksiz
(≤10 kademe sabit); doğrulama backend'de.

## R7 — Test güncellemesi: Commission.Api.Tests yeniden hizalanır

**Decision**: Mevcut 20 test `Create(merchantId, rate, fee)` imzasına bağlı — kademeli imzaya
taşınır; yeni testler: tablo doğrulama matrisi (boş, 0'dan başlamayan, artmayan, tavan aşımı,
>10 kademe), kademe seçimi (iç/sınır/açık uç), tek-kademe eşdeğerliği (SC-004), efektif hesap
kademeli örneklerle (spec SC-002 sayıları).

**Rationale**: Saf domain test politikası (023/024 deseni) korunur.