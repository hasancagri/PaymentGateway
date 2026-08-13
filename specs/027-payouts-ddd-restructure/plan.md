# Implementation Plan: Payouts Yapısal DDD Geçişi

**Branch**: `027-payouts-ddd-restructure` | **Date**: 2026-08-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/027-payouts-ddd-restructure/spec.md`

## Summary

`Commission.Api/Domains/Payouts/` altındaki 8 iyzico wire/istemci tipi (4 resource `: ProviderResourceV2`
canlı `/reporting/settlement/*` + `/crossbooking/*` HTTP; 2 `BaseRequestV2` PKI istek; 2 nested DTO)
davranışsız olarak `Domains/` içinde duruyor — CP.VPOS-sınırı + İlke II ihlali. Bu iş (025/026 deseniyle
birebir) material'i sağlayıcı sınırına (`Commission.Api/Provider/Payout/`, namespace
`Commission.Api.Provider.Payout`) taşır, `Domains/Payouts/` klasörünü dağıtır. Davranış YOK; 024
`CommissionPolicy` dokunulmaz. 026+027 sonrası `Commission.Api/Domains/` sağlayıcı-türeyen tipten TAM
arınır. Doğrulama: grep + `dotnet build` 0 hata + `Commission.Api.Tests` (20/20) yeşil.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable`, `ImplicitUsings` açık)
**Primary Dependencies**: mevcut `Commission.Api/Provider/` çekirdeği. Yeni paket YOK.
**Storage**: Yok — kalıcılık dokunulmaz.
**Testing**: mevcut `tests/Commission.Api.Tests` (20 test) DEĞİŞMEDEN yeşil (guardrail).
**Target Platform**: Commission.Api (Aspire).
**Project Type**: web-service (tek BC) — iç yapısal düzenleme.
**Performance Goals**: Yok (derleme-zamanı geçiş).
**Constraints**: Davranış eklenmez (FR-004/SC-005). 024 domain + yüzey dokunulmaz (FR-005). Sağlayıcı
tipleri `Domains/` sınırını geçmez.
**Scale/Scope**: 8 tip taşınır, 1 klasör dağıtılır, 1 GlobalUsings satırı. Domain değişmez.

## Constitution Check

| İlke | Durum | Not |
|------|-------|-----|
| I. BC İzolasyonu | PASS | Tümü Commission.Api içinde. |
| II. Zengin Domain / anti-anemik | PASS (ihlali GİDERİR) | Anemik wire `Domains/`'den çıkar → sağlayıcı sınırı (anemik-DTO orada doğru). 024 domain değişmez. |
| III. Vertical Slice + CQRS | PASS | Yeni slice yok. |
| IV. Result Pattern | PASS | Yeni handler/metot yok. |
| V. Merkezi Kimlik + Açık Yetki | PASS | Yeni endpoint YOK (SC-005). |
| VI. Spec-Driven | PASS | specify → plan → tasks → implement. |
| CP.VPOS-sınırı | PASS (ENFORCE eder) | İşin amacı. 026+027 sonrası Domains TAM temiz. |
| Tech kısıtları | PASS | .NET 10, yeni paket yok. |

**Violation yok** → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/027-payouts-ddd-restructure/
├── plan.md · research.md · data-model.md · quickstart.md
├── checklists/requirements.md   # (16/16)
└── tasks.md
```

> **contracts/ YOK**: dış arayüz/endpoint eklenmez/değişmez.

### Source Code (repository root)

```text
src/services/Commission.Api/
├── Provider/
│   ├── (mevcut çekirdek + Reporting/ [026])          # DEĞİŞMEZ
│   └── Payout/                                        # YENİ (iyzico settlement/crossbooking grubu)
│       ├── PayoutCompletedTransactionList.cs          # TAŞINDI (: ProviderResourceV2, /reporting/settlement/payoutcompleted)
│       ├── PayoutCompletedTransaction.cs              # TAŞINDI (nested DTO)
│       ├── BouncedBankTransferList.cs                 # TAŞINDI (: ProviderResourceV2, /reporting/settlement/bounced)
│       ├── BankTransfer.cs                            # TAŞINDI (nested DTO)
│       ├── CrossBookingToSubMerchant.cs               # TAŞINDI (: ProviderResourceV2, /crossbooking/send)
│       ├── CrossBookingFromSubMerchant.cs             # TAŞINDI (: ProviderResourceV2, /crossbooking/receive)
│       ├── CreateCrossBookingRequest.cs               # TAŞINDI (: BaseRequestV2, PKI)
│       └── RetrieveTransactionsRequest.cs             # TAŞINDI (: BaseRequestV2, PKI)
├── Domains/
│   ├── CommissionPolicies/                            # DEĞİŞMEZ (024)
│   └── Payouts/                                        # DAĞITILDI (silinir)
└── GlobalUsings.cs                                    # Domains.Payouts → Provider.Payout

tests/Commission.Api.Tests/                            # DEĞİŞMEZ — 20/20 yeşil
```

**Structure Decision**: Namespace `Commission.Api.Provider.Payout` (iyzico settlement/crossbooking
payout grubu; tip adıyla çakışmaz). Domain değişmez. Bu, Reporting (026) yanında ikinci Provider alt-
klasörü; `Domains/` bundan sonra yalnız gerçek domain (`CommissionPolicies`) içerir.

## Complexity Tracking

*Violation yok — boş.*
