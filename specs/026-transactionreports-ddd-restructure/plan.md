# Implementation Plan: TransactionReports Yapısal DDD Geçişi

**Branch**: `026-transactionreports-ddd-restructure` | **Date**: 2026-08-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/026-transactionreports-ddd-restructure/spec.md`

## Summary

`Commission.Api/Domains/TransactionReports/` altındaki 13 iyzico wire/istemci tipi (`TransactionReport
/TransactionReportResource`, `TransactionDetail/TransactionDetailResource : ProviderResourceV2` +
canlı `/v2/reporting/payment/transactions` HTTP; 3 `BaseRequestV2` PKI istek; 6 nested DTO) davranışsız
olarak `Domains/` içinde duruyor — CP.VPOS-sınırı + İlke II ihlali. Bu iş (025 SubMerchants deseniyle
birebir) material'i sağlayıcı sınırına (`Commission.Api/Provider/Reporting/`, namespace
`Commission.Api.Provider.Reporting`) taşır ve `Domains/TransactionReports/` klasörünü dağıtır.
Davranış (gerçek rapor çekimi, 024'e gerçek maliyet beslemesi) BU İŞTE YOK; 024 `CommissionPolicy`
dokunulmaz. Doğrulama: grep + `dotnet build` 0 hata + mevcut `Commission.Api.Tests` (20/20) yeşil.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable`, `ImplicitUsings` açık)

**Primary Dependencies**: mevcut `Commission.Api/Provider/` çekirdeği (`BaseRequestV2`,
`ProviderResourceV2`, `RestHttpClientV2`, `ProviderOptions`, PKI/hash). Yeni paket YOK.

**Storage**: Yok — kalıcılık dokunulmaz (`CommissionPolicy` document değişmez).

**Testing**: mevcut `tests/Commission.Api.Tests` (xUnit, 20 test) — DEĞİŞMEDEN yeşil kalır (regresyon
guardrail). Yeni test yok; doğrulama grep + build + mevcut testler.

**Target Platform**: Commission.Api (Aspire mikroservis)

**Project Type**: web-service (tek BC) — iç yapısal düzenleme; dış yüzey + 024 domain değişmez

**Performance Goals**: Yok (derleme-zamanı geçiş)

**Constraints**: Davranış eklenmez (FR-004/SC-005). 024 `CommissionPolicy` domain'i + dış yüzey
dokunulmaz (FR-005). Sağlayıcı tipleri `Domains/` sınırını geçmez (CP.VPOS-sınırı).

**Scale/Scope**: 13 tip taşınır (2 resource+türev, 3 PKI istek, 6 nested DTO), 1 klasör dağıtılır,
1 GlobalUsings satırı güncellenir. Domain kodu değişmez.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden kontrol.*

| İlke | Durum | Not |
|------|-------|-----|
| I. BC İzolasyonu | PASS | Tümü Commission.Api içinde; tipler BC dışına çıkmaz. |
| II. Zengin Domain / anti-anemik | PASS (ihlali GİDERİR) | Anemik wire tipleri `Domains/`'den çıkıp sağlayıcı sınırına — anemik-DTO orada DOĞRU. 024 domain (`CommissionPolicy`) zaten zengin, değişmez. |
| III. Vertical Slice + CQRS | PASS | Yeni slice yok. 024 slice'ları değişmez. |
| IV. Result Pattern | PASS | Yeni handler/metot yok. |
| V. Merkezi Kimlik + Açık Yetki | PASS | Yeni endpoint YOK (SC-005). |
| VI. Spec-Driven | PASS | specify → plan → tasks → implement. |
| CP.VPOS-sınırı | PASS (ENFORCE eder) | İşin amacı: sağlayıcı tiplerini domain sınırından çıkarmak. |
| Tech kısıtları | PASS | .NET 10, yeni paket yok. |

**Violation yok** → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/026-transactionreports-ddd-restructure/
├── plan.md · research.md · data-model.md · quickstart.md
├── checklists/requirements.md   # (16/16)
└── tasks.md
```

> **contracts/ YOK**: dış arayüz/endpoint eklenmez/değişmez (SC-005).

### Source Code (repository root)

```text
src/services/Commission.Api/
├── Provider/
│   ├── (mevcut çekirdek)                            # DEĞİŞMEZ
│   └── Reporting/                                    # YENİ (iyzico /v2/reporting grubu)
│       ├── TransactionReport.cs                       # TAŞINDI (resource + canlı Retrieve HTTP)
│       ├── TransactionReportResource.cs               # TAŞINDI (: ProviderResourceV2)
│       ├── TransactionReportItem.cs                   # TAŞINDI (nested DTO)
│       ├── TransactionDetail.cs                        # TAŞINDI
│       ├── TransactionDetailResource.cs               # TAŞINDI (: ProviderResourceV2)
│       ├── TransactionDetailItem.cs                   # TAŞINDI
│       ├── TransactionDetailCancelItem.cs             # TAŞINDI
│       ├── PaymentTxDetailItem.cs                     # TAŞINDI
│       ├── RefundDetailItem.cs                        # TAŞINDI
│       ├── ConvertedPayout.cs                         # TAŞINDI
│       ├── RetrieveTransactionReportRequest.cs        # TAŞINDI (: BaseRequestV2, PKI)
│       ├── RetrieveScrollTransactionReportRequest.cs  # TAŞINDI
│       └── RetrieveTransactionDetailRequest.cs        # TAŞINDI
├── Domains/
│   ├── CommissionPolicies/                           # DEĞİŞMEZ (024)
│   ├── Payouts/                                       # DEĞİŞMEZ (ayrı geçiş)
│   └── TransactionReports/                            # DAĞITILDI (silinir)
└── GlobalUsings.cs                                   # Domains.TransactionReports → Provider.Reporting

tests/Commission.Api.Tests/                            # DEĞİŞMEZ — 20/20 yeşil (guardrail)
```

**Structure Decision**: Namespace `Commission.Api.Provider.Reporting` (iyzico `/v2/reporting/...`
gruplaması; klasör adı bir tip adıyla çakışmaz). Domain değişmez; Payouts ayrı geçiş.

## Complexity Tracking

*Violation yok — boş.*
