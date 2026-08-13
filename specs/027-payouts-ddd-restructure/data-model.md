# Phase 1 Data Model: Payouts Yapısal DDD Geçişi

Yeni domain entity ÜRETİLMEZ — 8 wire tipi sağlayıcı sınırına taşınır, 024 domain dokunulmaz.

## Taşıma envanteri (Domains/Payouts → Provider/Payout)

Namespace: `Commission.Api.Domains.Payouts` → `Commission.Api.Provider.Payout` (8 tip).

| # | Tip | Base | Rol / iyzico ucu |
|---|-----|------|-------------------|
| 1 | `PayoutCompletedTransactionList` | `ProviderResourceV2` | `/reporting/settlement/payoutcompleted` Retrieve |
| 2 | `PayoutCompletedTransaction` | (yok) | nested DTO — tamamlanan payout kaydı |
| 3 | `BouncedBankTransferList` | `ProviderResourceV2` | `/reporting/settlement/bounced` Retrieve |
| 4 | `BankTransfer` | (yok) | nested DTO — banka transfer kaydı |
| 5 | `CrossBookingToSubMerchant` | `ProviderResourceV2` | `/crossbooking/send` |
| 6 | `CrossBookingFromSubMerchant` | `ProviderResourceV2` | `/crossbooking/receive` |
| 7 | `CreateCrossBookingRequest` | `BaseRequestV2` | PKI imzalı istek |
| 8 | `RetrieveTransactionsRequest` | `BaseRequestV2` | PKI imzalı istek |

Sonuç: `Domains/Payouts/` BOŞALIR → silinir. 026+027 sonrası `Domains/` altında `BaseRequestV2`/
`ProviderResourceV2` türeyen tip HİÇ KALMAZ (SC-001).

## Domain — DEĞİŞMEZ (024)

| Domain öğesi | Durum |
|--------------|-------|
| `CommissionPolicy` + `MarginRule`/`EffectiveCommission` | DOKUNULMAZ |
| `Domains/CommissionPolicies/` diff | 0 (SC-005) |

## Sınır çevirisi (davranış spec'ine bırakılan — BU İŞTE YOK)

- iyzico'dan gerçek payout/settlement çekme (`PayoutCompletedTransactionList.Retrieve` canlı çağrı).
- Cross-booking icrası (`CrossBookingToSubMerchant` canlı çağrı).
- Payout/settlement verisi → domain mutabakat temsili (davranış).

## Doğrulama kuralları

- `Domains/` altında sağlayıcı-türeyen = 0 (SC-001, TAM temiz).
- Aggregate-klasör tek-kök: yalnız `CommissionPolicies/CommissionPolicy.cs` (SC-002).
- `Provider/Payout/` 8 dosya; `Domains/Payouts/` yok (SC-004).
- `Domains/CommissionPolicies/` diff = 0, yeni endpoint = 0 (SC-005).
- Build 0 hata + Commission testleri 20/20 (SC-003).
