# Specification Quality Checklist: PaymentLink Üretimi (Pay-by-Link ilk dilim)

**Purpose**: Planlamadan önce spec tamlık/kalite doğrulaması
**Created**: 2026-10-20
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

> Not: Birkaç kod-izi atfı bilinçli (traceability) — `HostedPaymentSession` (088 tohumu), `payment.write`
> (geçici yetki kapsamı), `PROVIDER_UNAVAILABLE` (stub kodu), 041 referansı. 088 spec deseniyle tutarlı;
> framework/dil/şema dökümü yok. Domain adları (PaymentLink, TTL, hosted sayfa) kapsamı tanımlayan özneler.

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Kapsam net sınırlı: link üretimi + hosted güven bağlamı + yaşam döngüsü. Banka/simülatör + çekim + 3DS/
  NonSecure + ödeme-modeli seçimi KAPSAM DIŞI (banka dilimi). "Öde" = PROVIDER_UNAVAILABLE stub.
- Varsayımlarla kapatılan açık noktalar `/speckit-clarify`'da teyit/değişebilir: TTL süresi + "süre dolarken
  sayfada" grace, terminal-sonrası re-create, abandon→Expired tetiği (lazy vs watchdog), klasik SMS/email
  teslim yolu (bu dilim yalnız sohbet/S2S), çift-create yarışı.
- SC'ler ölçülebilir + teknoloji-bağımsız (süre/yüzde/sıfır-sızıntı). Domain-TDD (İLKE VI) uygundur.
- Hazır: `/speckit-clarify` (açık varsayımları soruya çevirir) ya da küçük-ölçekse doğrudan `/speckit-plan`.
