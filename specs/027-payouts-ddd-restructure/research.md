# Phase 0 Research: Payouts Yapısal DDD Geçişi

025/026 ile birebir desen; kararlar yapısal. Zemin [[decisions_iyzico_sdk_ddd_adaptation]].

## R1 — Wire/istemci tipleri nereye taşınır

- **Decision**: 8 tip → `Commission.Api/Provider/Payout/`, namespace `Commission.Api.Provider.Payout`.
  `Domains/Payouts/` silinir.
- **Rationale**: iyzico payout/settlement/crossbooking wire/istemci malzemesi (`BaseRequestV2`/
  `ProviderResourceV2` türevi, PKI, canlı HTTP `/reporting/settlement/*` + `/crossbooking/*`).
  CP.VPOS-sınırı: sağlayıcı tipleri `Domains/` geçmez. Klasör adı `Payout` (iyzico payout grubu; tip
  adıyla çakışmaz — `PayoutCompletedTransaction` ≠ segment `Payout`).
- **Alternatives**: `Settlement` (crossbooking'i kapsamaz); `Domains/`'de tutmak (ihlal).

## R2 — Nested DTO + resource+çağrı birleşik deseni

- **Decision**: Nested DTO'lar (`PayoutCompletedTransaction`, `BankTransfer`) ve resource'lar
  (DTO+static HTTP birleşik) AYNEN sağlayıcı tarafına taşınır; bölünmez.
- **Rationale**: SDK idiomatik deseni; 025/026 (Onboarding/Reporting) ile tutarlı. Bölmek gold-plating.
- **Alternatives**: DTO/çağrı ayrımı (tutarsız, YAGNI).

## R3 — Klasör dağıtımı

- **Decision**: `Domains/Payouts/` tamamen DAĞITILIR; geride domain tip kalmaz. 026+027 sonrası
  `Domains/` yalnız `CommissionPolicies` içerir.
- **Rationale**: Aggregate-klasör kuralı: klasörde aggregate YOK (yalnız wire). Dağıtmak kuralı geri
  getirir; `Domains/` sağlayıcı-türeyenden TAM arınır (SC-001/SC-002).
- **Alternatives**: Boş klasör bırakmak (kural ihlali).

## R4 — Referans/derleme güvenliği

- **Decision**: Taşıma güvenli — Payout tipleri hiçbir yerde KULLANILMAZ. Tek tip referansı
  `GlobalUsings.cs` (`global using Commission.Api.Domains.Payouts;`). 024'teki `NetPayout`/
  `MerchantPayoutAmount`/`SubMerchantPayoutAmount` yalnız "Payout" SUBSTRING'i (tip kullanımı DEĞİL).
  Çapraz-ref yok. GlobalUsings satırı `Commission.Api.Provider.Payout`'a güncellenir.
- **Rationale**: `grep -rn` (folder dışı): yalnız GlobalUsings + substring alan adları. Namespace
  değişimi derlemeyi kırmaz.
- **Alternatives**: Yok — olgusal.

## R5 — İsim benzerliği: CrossBooking*SubMerchant vs 025 SubMerchant

- **Decision**: `CrossBookingToSubMerchant`/`CrossBookingFromSubMerchant` tipleri "SubMerchant"
  adını içerir ama 025'te Merchant.Api'ye taşınan `SubMerchant`'tan BAĞIMSIZ (farklı BC, farklı tip).
  Bu tipler Payout material'i olarak Commission.Api/Provider/Payout'a gider.
- **Rationale**: İsim benzerliği yalnızca kelime; çapraz-BC referans yok (Commission ↛ Merchant).
- **Alternatives**: Yok.

## R6 — Taşıma yöntemi

- **Decision**: `git mv` + taşınan dosyalarda namespace `Commission.Api.Domains.Payouts` →
  `Commission.Api.Provider.Payout`. Base tipler (`Commission.Api.Provider`) child namespace'ten görünür.
- **Rationale**: 026 ile aynı; tümü aynı namespace → birlikte taşınınca intra-referans çözülür.
- **Alternatives**: Elle yeniden yaz (hataya açık).

## Çözülmemiş NEEDS CLARIFICATION

Yok. Spec 0 marker; R1–R6 sabitledi.
