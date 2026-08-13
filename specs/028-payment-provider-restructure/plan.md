# Implementation Plan: Payment.Api iyzico Wire Material — Yapısal DDD Geçişi

**Branch**: `028-payment-provider-restructure` | **Date**: 2026-08-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/028-payment-provider-restructure/spec.md`

## Summary

`Payment.Api/Domains/{Payments(28),Installments(6),StoredCards(6)}` = 40 iyzico wire/istemci tipi
davranışsız olarak `Domains/` içinde duruyor — CP.VPOS-sınırı + İlke II ihlali. Payment.Api'nin gerçek
domain'i YOK (022 pivot). Bu iş (025/026/027 deseniyle birebir) üç klasörü sağlayıcı sınırına
(`Payment.Api/Provider/{Payments,Installments,StoredCards}/`, namespace `Payment.Api.Provider.X`)
taşır, `Domains/` klasörlerini dağıtır → `Payment.Api/Domains/` TAMAMEN boşalır. Davranış YOK. Doğrulama:
grep + `dotnet build` 0 hata + diğer BC testleri (Merchant 30 + Commission 20) yeşil.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: mevcut `Payment.Api/Provider/` çekirdeği. Yeni paket YOK.
**Storage**: Yok.
**Testing**: Payment.Api test projesi YOK; doğrulama = build + diğer BC testleri + grep.
**Target Platform**: Payment.Api (Aspire, ara durum — endpoint/aggregate yok).
**Project Type**: web-service (tek BC).
**Performance Goals**: Yok (derleme-zamanı geçiş).
**Constraints**: Davranış eklenmez (FR-005/SC-004). Sağlayıcı tipleri `Domains/` geçmez.
**Scale/Scope**: 40 tip taşınır (3 klasör), 3 klasör dağıtılır, 3 GlobalUsings satırı. Domain yok.

## Constitution Check

| İlke | Durum | Not |
|------|-------|-----|
| I. BC İzolasyonu | PASS | Tümü Payment.Api içinde. |
| II. Zengin Domain / anti-anemik | PASS (ihlali GİDERİR) | Anemik wire `Domains/`'den çıkar → sağlayıcı sınırı. Payment gerçek domain'i sonra (charge spec'i) kurulur. |
| III. Vertical Slice + CQRS | PASS | Yeni slice yok. |
| IV. Result Pattern | PASS | Yeni handler/metot yok. |
| V. Merkezi Kimlik + Açık Yetki | PASS | Yeni endpoint YOK (SC-004). |
| VI. Spec-Driven | PASS | specify → plan → tasks → implement. |
| CP.VPOS-sınırı | PASS (ENFORCE eder) | İşin amacı; Payment.Api/Domains TAM temizlenir. |
| Tech kısıtları | PASS | .NET 10, yeni paket yok. |

**Violation yok** → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/028-payment-provider-restructure/
├── plan.md · research.md · data-model.md · quickstart.md
├── checklists/requirements.md   # (16/16)
└── tasks.md
```

> **contracts/ YOK**: endpoint eklenmez/değişmez.

### Source Code (repository root)

```text
src/services/Payment.Api/
├── Provider/
│   ├── (mevcut çekirdek)                              # DEĞİŞMEZ
│   ├── Payments/         (28 dosya)                    # YENİ — TAŞINDI (namespace Payment.Api.Provider.Payments)
│   ├── Installments/     (6 dosya)                     # YENİ — TAŞINDI (Payment.Api.Provider.Installments)
│   └── StoredCards/      (6 dosya)                     # YENİ — TAŞINDI (Payment.Api.Provider.StoredCards)
├── Domains/                                            # TAMAMEN BOŞALIR (3 klasör silinir)
└── GlobalUsings.cs                                    # 3 satır: Domains.X → Provider.X

tests/  (Payment.Api testi yok)                         # Merchant 30 + Commission 20 yeşil kalır
```

**Structure Decision**: Namespace `Payment.Api.Provider.{Payments,Installments,StoredCards}` (klasör
adları origin'le aynı çoğul → `Payment` tipiyle segment çakışması yok). Payment.Api gerçek domain'i
olmadığından `Domains/` boşalır; charge akışı domain'i sonraki davranış spec'inde doğar.

## Complexity Tracking

*Violation yok — boş.*
