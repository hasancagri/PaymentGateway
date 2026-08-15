# Implementation Plan: Kayıtlı Kartla Ödeme (NonSecure, Taksitli)

**Branch**: `033-saved-card-payment` | **Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/033-saved-card-payment/spec.md`

## Summary

Gateway'in ilk gerçek **çekim** yeteneği: 032'nin iyzico Saklı Kart altyapısı (cardUserKey/cardToken)
üstüne, kayıtlı kartla CVC-siz NonSecure ödeme. İki Payment.Api ucu — taksit sorgusu
(`/payment/iyzipos/installment`) + çekim (`/payment/auth`). Başarıda `Payment` aggregate kaydı +
`PaymentChargedEvent` (iyzico maliyeti taşır). Yeni `payment.charge` scope (Active-only, cards.write
deseni). Efektif komisyon Payment BC'de hesaplanmaz (event-driven, Commission BC — BC izolasyonu).
Sub-merchant split yok (tek-seviye merchant). US1/US2 gateway (curl), US3 ECommerce checkout uçtan
uca. Kararlar: [research.md](research.md) R1-R9.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: Payment.Api yığını (Marten, Wolverine, Minimal API) + Provider/Payments
wire tipleri (020'den, uyuyor) + Provider çekirdeği (spike'la kanıtlı). iyzico sandbox (gerçek HTTP).
Yeni paket YOK.

**Storage**: paymentDb — yeni `mt_doc_payment` document (Marten default Guid Id); migration yok.

**Testing**: xUnit `tests/Payment.Api.Tests` — Payment aggregate saf testleri; iyzico charge/installment
quickstart canlı (sandbox test kartı, 032 ile saklanmış). Merchant (47) + Commission (31) regresyon.

**Target Platform**: Aspire AppHost; Payment.Api :5201; Identity :5101; iyzico sandbox. Tüketici
US1/US2 curl, US3 ECommerce.

**Project Type**: Payment BC yeni aggregate + 2 slice + scope plumbing (3 servise dokunur: Common +
Identity.Server + Payment.Api) + US3 ECommerce (ayrı repo). İlk canlı **charge** entegrasyonu.

**Performance Goals**: Yok (dev); iyzico senkron.

**Constraints**: CVC/CardNumber çekimde YOK (saklı kart); PAN gateway'de hiç (032); sağlayıcı tipleri
BC dışına sızmaz (handler map); efektif komisyon Payment BC'de HESAPLANMAZ (FR-008); split yok;
sandbox key user-secrets; charge yalnız Active merchant (fail-closed).

**Scale/Scope**: 1 aggregate + 1 enum + 2 slice + endpoint-ext + event + scope (4 nokta) + Program
wire ≈ 10 dosya (gateway) + ECommerce US3 (client + checkout ~4 dosya).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Değerlendirme | Durum |
|---|---|---|
| I. BC İzolasyonu | Payment BC yeni aggregate; Commission'ı OKUMAZ (efektif komisyon event-driven — FR-008). StoredCard aynı BC (Payment). ECommerce ayrı sistem (HTTP+OAuth). iyzico tipleri handler'da map, aggregate görmez. | ✅ |
| II. Zengin Domain | `Payment` anemik değil: `Succeeded`/`Failed` fabrikaları (çekim sonucu invariant'ı); iyzico çağrısı handler'da (yan etki). | ✅ |
| III. Vertical Slice + CQRS | 2 slice (charge command + installment query); `[Transactional]` charge'da; repository yok. | ✅ |
| IV. Result Pattern | `Succeeded`/`Failed` → `ResultDomain`; handler `FeatureObjectResultModel<T>`; iyzico hatası resource-kodlu. | ✅ |
| V. Kimlik + Açık Yetki | Yeni `payment.charge` scope (Active-only, fail-closed) — anayasa V "Active tam demet charge dahil" (013) öngörüsünün somutlaşması; kiracı çift-kapı (MerchantScoped + StoredCard.MerchantId). PAN/CVC yok. Secret user-secrets. | ✅ |

**Gate sonucu**: GEÇTİ. `payment.charge` scope = anayasanın planladığı genişleme (ihlal değil).

## Project Structure

### Documentation (this feature)

```text
specs/033-saved-card-payment/
├── plan.md · research.md · data-model.md · quickstart.md
└── contracts/payment-api.md
```

### Source Code (repository root)

```text
# PaymentGateway (gateway)
src/services/Payment.Api/Domains/Payments/                 # YENİ
├── Payment.cs · PaymentStatus.cs · PaymentEndpointExtension.cs
└── Features/{Commands/ChargePayment.cs, Queries/InstallmentOptions.cs}
src/services/Payment.Api/Program.cs                        # payment.charge auth + PaymentChargedEvent publish + Payments endpoint grup
src/others/Shared/IntegrationEvents.cs                     # +PaymentChargedEvent
src/others/Common/Utils/Constants/AuthorizationScopes.cs   # +PaymentCharge
src/others/Identity.Server/Config.cs                       # ScopeResources +payment.charge
src/others/Identity.Server/EventHandlers/MerchantClientEventHandler.cs  # Active demeti +payment.charge
tests/Payment.Api.Tests/PaymentTests.cs                    # aggregate Succeeded/Failed

# ECommerce (US3, ayrı repo — implement'te keşif)
GatewayPaymentClient (charge + installment) + MerchantTokenProvider scope += payment.charge
+ checkout taksit seçimi/charge akışı + Order "ödendi"
```

**Structure Decision**: Payment BC'ye yeni aggregate; scope plumbing 3 servise dokunur (cards.write
emsali). ECommerce US3 ayrı repo, gateway sözleşmesini tüketir.

## Complexity Tracking

> İhlal yok. `payment.charge` scope anayasa-planlı genişleme; efektif komisyonun ertelenmesi BC
> izolasyonunun sonucu (FR-008), sapma değil.
