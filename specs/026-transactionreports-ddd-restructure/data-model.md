# Phase 1 Data Model: TransactionReports Yapısal DDD Geçişi

Yeni domain entity ÜRETİLMEZ — 13 wire tipi sağlayıcı sınırına taşınır, 024 domain dokunulmaz.
"Data model" = taşıma envanteri.

## Taşıma envanteri (Domains/TransactionReports → Provider/Reporting)

Namespace: `Commission.Api.Domains.TransactionReports` → `Commission.Api.Provider.Reporting` (13 tip).

| # | Tip | Base | Rol |
|---|-----|------|-----|
| 1 | `TransactionReport` | `TransactionReportResource` | canlı `/v2/reporting/payment/transactions` Retrieve + `GetQueryParams` helper |
| 2 | `TransactionReportResource` | `ProviderResourceV2` | wire yanıt zarfı |
| 3 | `TransactionReportItem` | (yok) | nested DTO — işlem satırı (IyzicoCommission/IyzicoFee/Installment/PaidPrice...) |
| 4 | `TransactionDetail` | `TransactionDetailResource` | canlı detay Retrieve |
| 5 | `TransactionDetailResource` | `ProviderResourceV2` | wire yanıt zarfı |
| 6 | `TransactionDetailItem` | (yok) | nested DTO |
| 7 | `TransactionDetailCancelItem` | (yok) | nested DTO |
| 8 | `PaymentTxDetailItem` | (yok) | nested DTO — MerchantCommissionRate/IyziCommissionFee... |
| 9 | `RefundDetailItem` | (yok) | nested DTO |
| 10 | `ConvertedPayout` | (yok) | nested DTO |
| 11 | `RetrieveTransactionReportRequest` | `BaseRequestV2` | PKI imzalı istek |
| 12 | `RetrieveScrollTransactionReportRequest` | `BaseRequestV2` | PKI imzalı istek |
| 13 | `RetrieveTransactionDetailRequest` | `BaseRequestV2` | PKI imzalı istek |

Sonuç: `Domains/TransactionReports/` BOŞALIR → silinir. `Domains/` altında `BaseRequestV2`/
`ProviderResourceV2` türeyen tip KALMAZ (SC-001).

## Domain — DEĞİŞMEZ (024)

| Domain öğesi | Durum |
|--------------|-------|
| `CommissionPolicy` aggregate + `MarginRule`/`EffectiveCommission` VO | DOKUNULMAZ |
| `CalculateEffectiveCommission` maliyet girdisi | string kalır (rapor tipini referanslamaz) |
| `Domains/CommissionPolicies/` diff | 0 (SC-005) |

## Sınır çevirisi (davranış spec'ine bırakılan — BU İŞTE YOK)

- iyzico'dan gerçek rapor çekme (`TransactionReport.Retrieve` canlı çağrı).
- `TransactionReportItem.IyzicoCommission`+`IyzicoFee` → 024 `CalculateEffectiveCommission` gerçek
  maliyet beslemesi (şu an string girdi).

## Doğrulama kuralları

- `Domains/` altında sağlayıcı-türeyen = 0 (SC-001).
- Aggregate-klasör tek-kök korunur; `TransactionReports` listede yok (SC-002).
- `Provider/Reporting/` 13 dosya; `Domains/TransactionReports/` yok (SC-004).
- `Domains/CommissionPolicies/` diff = 0, yeni endpoint = 0 (SC-005).
- Build 0 hata + Commission testleri 20/20 yeşil (SC-003).
