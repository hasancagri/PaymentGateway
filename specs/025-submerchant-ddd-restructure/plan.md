# Implementation Plan: SubMerchants Yapısal DDD Geçişi

**Branch**: `025-submerchant-ddd-restructure` | **Date**: 2026-08-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/025-submerchant-ddd-restructure/spec.md`

## Summary

`Merchant.Api/Domains/SubMerchants/` altındaki beş iyzico wire/istemci tipi (`SubMerchant :
ProviderResourceV2` + canlı `/onboarding/submerchant` HTTP çağrıları; `Create/Update/
RetrieveSubMerchantRequest : BaseRequestV2` PKI imzalı; `SubMerchantType` enum) davranışsız olarak
`Domains/` içinde duruyor — anayasa CP.VPOS-sınırı + İlke II ihlali. Bu iş, material'i **sağlayıcı
sınırına** (`Merchant.Api/Provider/Onboarding/`) taşır, `Domains/SubMerchants/` klasörünü dağıtır.
Davranış (iyzico'ya gerçek kayıt akışı) BU İŞTE YOK. Domain temsili değişmez: 023 `Merchant`
aggregate'i sub-merchant bağını zaten domain-tarafında taşıyor (`SubMerchantKey` alanı + `MerchantType`
matrisi) — bu iş onu koruyup wire vocab'ı (`SubMerchantType`) sınıra ayırır; richer VO wiring, davranış
spec'ine kalır (YAGNI). Doğrulama: grep kuralları + `dotnet build` 0 hata + mevcut Merchant testleri
yeşil.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable`, `ImplicitUsings` açık)

**Primary Dependencies**: mevcut `Merchant.Api/Provider/` çekirdeği (`BaseRequestV2`,
`ProviderResourceV2`, `RestHttpClientV2`, `ProviderOptions`, PKI/hash yardımcıları). Yeni paket YOK.

**Storage**: Yok — bu iş kalıcılık dokunmaz (Marten şeması, `Merchant` document değişmez).

**Testing**: mevcut `tests/Merchant.Api.Tests` (xUnit saf domain) — DEĞİŞMEDEN yeşil kalır (regresyon
guardrail). Yeni davranış testi gerekmez; doğrulama grep + build + mevcut testler.

**Target Platform**: Merchant.Api (Aspire üzerinden koşan mikroservis)

**Project Type**: web-service (tek BC) — yalnız iç yapısal düzenleme, dış yüzey değişmez

**Performance Goals**: Yok (derleme-zamanı yapısal geçiş; çalışma-anı etkisi sıfır)

**Constraints**: Davranış eklenmez (FR-004/SC-005 — yeni uç/handler/iş-kuralı 0). Mevcut dış yüzey +
`Merchant` aggregate + 024 dokunulmaz (FR-006). Sağlayıcı tipleri `Domains/` sınırını geçmez
(CP.VPOS-sınırı).

**Scale/Scope**: 5 tip taşınır (1 resource+client, 3 request, 1 enum), 1 klasör dağıtılır, 1
GlobalUsings satırı güncellenir. Domain kodu değişmez.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden kontrol.*

| İlke | Durum | Not |
|------|-------|-----|
| I. BC İzolasyonu | PASS | Tümü Merchant.Api içinde; tipler BC dışına çıkmaz. Sağlayıcı material sınıra (Provider/) taşınır. |
| II. Zengin Domain / anti-anemik | PASS (ihlali GİDERİR) | Anemik wire tipleri `Domains/`'den çıkarılıp sağlayıcı sınırına konur — anemik-DTO orada DOĞRU (wire şekli). Domain (`Merchant`) zaten zengin, değişmez. Yeni aggregate yok (domain kavramı 023'te mevcut). |
| III. Vertical Slice + CQRS | PASS | Yeni slice yok (davranış yok). Mevcut slice'lar değişmez. |
| IV. Result Pattern | PASS | Yeni handler/aggregate metodu yok. |
| V. Merkezi Kimlik + Açık Yetki | PASS | Yeni endpoint YOK (SC-005). Yetki yüzeyi değişmez. |
| VI. Spec-Driven | PASS | specify → plan (bu) → tasks → implement. |
| CP.VPOS-sınırı | PASS (ENFORCE eder) | Bu işin bütün amacı: sağlayıcı tiplerini domain sınırından çıkarmak. |
| Tech kısıtları | PASS | .NET 10, yeni paket yok, TL-only alakasız. |

**Violation yok** → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/025-submerchant-ddd-restructure/
├── plan.md              # Bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1 (yapısal eşleme — yeni entity yok)
├── quickstart.md        # Phase 1 (grep + build + test doğrulama)
├── checklists/
│   └── requirements.md  # (mevcut, 16/16)
└── tasks.md             # /speckit-tasks
```

> **contracts/ YOK**: bu iş dış arayüz/endpoint eklemez veya değiştirmez (SC-005) — API kontratı
> üretilmez.

### Source Code (repository root)

```text
src/services/Merchant.Api/
├── Provider/
│   ├── (mevcut çekirdek: BaseRequestV2, ProviderResourceV2, RestHttpClientV2 ...)  # DEĞİŞMEZ
│   └── Onboarding/                                  # YENİ alt-klasör (iyzico /onboarding grubu)
│       ├── SubMerchant.cs                            # TAŞINDI (ProviderResourceV2 + Create/Update/Retrieve)
│       ├── CreateSubMerchantRequest.cs               # TAŞINDI (BaseRequestV2, PKI)
│       ├── UpdateSubMerchantRequest.cs               # TAŞINDI
│       ├── RetrieveSubMerchantRequest.cs             # TAŞINDI
│       └── SubMerchantType.cs                        # TAŞINDI (wire vocab enum)
├── Domains/
│   ├── Merchants/                                    # DEĞİŞMEZ (SubMerchantKey + MerchantType matrisi kalır)
│   └── SubMerchants/                                 # DAĞITILDI (klasör silinir — içi Provider'a taşındı)
└── GlobalUsings.cs                                   # güncelle: Domains.SubMerchants using kalkar,
                                                       #          Provider.Onboarding eklenir (gerekiyorsa)

tests/Merchant.Api.Tests/                             # DEĞİŞMEZ — yeşil kalır (regresyon guardrail)
```

**Structure Decision**: Yeni namespace `Merchant.Api.Provider.Onboarding` (iyzico'nun kendi
`/onboarding/submerchant` gruplamasını yansıtır; klasör adı `SubMerchant` yapılmaz — tip adıyla
çakışmasın diye `Onboarding`). Domain-tarafı temsili YENİ üretilmez: sub-merchant bağı 023
`Merchant` aggregate'inde zaten domain-konvansiyonuyla var (`SubMerchantKey` + `MerchantType`).
Wire vocab (`SubMerchantType`, iyzico UPPER değerleri) sağlayıcı sınırına gider; `MerchantType`↔
`SubMerchantType` çevirisi davranış spec'inin işi.

## Complexity Tracking

*Violation yok — boş.*
