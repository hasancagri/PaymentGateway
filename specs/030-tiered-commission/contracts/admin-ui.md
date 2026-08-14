# Contract: Admin CommissionPolicies Ekranı (030 revizyonu)

Ekran 029'da doğdu (`Pages/CommissionPolicies/Index`); kademeye uyarlanır. JS YOK (R6).

## Oluşturma formu

- Merchant dropdown (aynen)
- **Kademe grid'i**: 10 sabit satır — `Tiers[i].FromAmount` / `Tiers[i].RatePercent` /
  `Tiers[i].FixedFee` indeksli input'lar; ilk satır FromAmount=0 önerili dolu, kalanlar boş
- Tamamen boş satırlar post işleyicisinde atlanır; kalan satırlar sırayla `TierDto` listesine
  çevrilip API'ye gider (doğrulama backend'de; UI kural sızdırmaz)

## Güncelleme

- Satırdaki tek-alanlık mini form yerine: satırda **Tarife Düzenle** → aynı sayfada seçili
  politika için grid mevcut kademelerle dolu gelir (query param `merchantId`), kaydet →
  `PUT /margin`

## Liste

- `Oran`/`Sabit Ücret` kolonları yerine tek **Tarife** kolonu, kompakt gösterim:
  `0+: %2,5 + 1 TL · 1.000+: %2 + 1 TL · 10.000+: %1,8`
- Durum + Pasifleştir/Aktifleştir + merchant Detay linki aynen

## İstemci (`CommissionPolicyApiClient`)

- `CreateAsync(CreateCommissionPolicyRequest)` / `UpdateMarginAsync(merchantId, List<TierDto>)`
  yeni gövdeler; `CommissionPolicyItem.Tiers` listesi. `ChangeStatusAsync` aynen.