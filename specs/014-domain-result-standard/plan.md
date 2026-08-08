# Implementation Plan: Domain Sonuç Sarmalama Standardı (ResultDomain)

**Branch**: `014-domain-result-standard` | **Date**: 2026-08-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/014-domain-result-standard/spec.md`

## Summary

Handler'dan çağrılan aggregate davranış/fabrika metotlarını tek tip `ResultDomain`/`ResultDomain<T>`
dönecek şekilde refactor et; saf getter/sorgu muaf. Klasör kurallarını (aggregate-per-folder,
`ValueObjects/`) ve sonuç sözleşmesini `CLAUDE.md`'ye yazılı kural yap. PaymentGateway ayağı; ECommerce
paralel `031` spec'inde. Yaklaşım: envanter (call-site tabanlı) → metot imzasını sar → tüm handler
çağıranlarını `IsSuccess/Data/Messages` desenine güncelle → testleri güncelle → build+test yeşil.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: Common (`ResultDomain`/`ResultDomain<T>`, `MessageItem`), Marten, Wolverine

**Storage**: PostgreSQL (Marten) — bu refactor için davranışsal etkisi yok (imza/dönüş tipi değişimi)

**Testing**: xUnit saf domain birim testleri (`tests/Merchant.Api.Tests`, `tests/Commission.Api.Tests`)

**Target Platform**: Linux/host — mikroservis BC'leri (Aspire orkestrasyon)

**Project Type**: web-service (çok-BC mikroservis çözümü, `PaymentGateway.slnx`)

**Performance Goals**: N/A — davranış değişmez, yalnız dönüş sözleşmesi tek tipleşir

**Constraints**: Davranış eşdeğerliği (refactor); mevcut testler yeşil kalmalı; CP.VPOS tipleri slice
sınırını geçmez (dokunulmaz)

**Scale/Scope**: ~11 aggregate (Merchant, RegisterRequest, DomainControlChallenge, ActivationTicket,
OnboardingNotification, SettlementAccount, PosAccount, Commission×3, Reference×4) — yalnız handler'dan
çağrılan ham-dönen metotlar hedeflenir (kesin liste Phase 1 data-model'de, envanter ajanından)

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar.*

- **Result pattern (anayasa normu)**: Bu özellik mevcut Result pattern'i domain katmanında zorunlu
  ve tek tip hale getirir — normu güçlendirir, ihlal etmez. ✓
- **Vertical Slice + CQRS**: Handler'lar `[Transactional]` + `IDocumentSession`, sonuç
  `FeatureObjectResultModel<T>`/`ResultDomain` — bu desene sadık kalınır; slice yapısı değişmez. ✓
- **Zengin aggregate**: Davranış aggregate'te kalır; yalnız dönüş tipi sarılır. ✓
- **İzole altyapı istisnaları**: Identity.Server EF Core / Reference read-model gibi istisnalar
  etkilenmez. ✓
- **Yeni karmaşa yok**: Yeni soyutlama/paket/katman eklenmez; mevcut `ResultDomain` kullanılır.

**Sonuç: GEÇTİ — ihlal yok, Complexity Tracking gereksiz.**

## Project Structure

### Documentation (this feature)

```text
specs/014-domain-result-standard/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — karar kayıtları (call-site kapsamı, outcome-enum, factory)
├── data-model.md        # Phase 1 — hedef metot envanteri (aggregate × metot × handler × test)
├── quickstart.md        # Phase 1 — doğrulama: build + domain testleri
├── checklists/
│   └── requirements.md  # spec kalite checklist'i (mevcut)
└── tasks.md             # Phase 2 (/speckit-tasks — bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/services/Merchant.Api/Domains/
├── Merchants/Merchant.cs                              # TryActivate() → ResultDomain
├── DomainControlChallenges/DomainControlChallenge.cs  # Verify() → ResultDomain<ChallengeOutcome>
├── ActivationTickets/ActivationTicket.cs              # Issue() fabrika (handler'dan çağrılıyorsa)
├── OnboardingNotifications/OnboardingNotification.cs  # Create() fabrika
├── RegisterRequests/RegisterRequest.cs
└── SettlementAccounts/SettlementAccount.cs
src/services/Commission.Api/Domains/**                 # envanter sonucu
src/services/Payment.Api/Domains/**                    # PosAccount (GetCommissionRate = EXEMPT getter)
src/services/Reference.Api/Domains/**                  # event-only; handler-çağrılı ham metot beklenmiyor

# Çağıran güncellemeleri: her aggregate'in Features/**/*.cs handler'ları
# Test güncellemeleri: tests/Merchant.Api.Tests, tests/Commission.Api.Tests
CLAUDE.md                                               # 3 kural yazılı (FR-010)
```

**Structure Decision**: Mevcut çok-BC mikroservis düzeni korunur. Yeni dosya/klasör (spec artefaktları
hariç) eklenmez; değişiklik yalnız aggregate dönüş imzaları + handler/test çağıranları + `CLAUDE.md`.

## Complexity Tracking

> Constitution Check ihlali yok — bu bölüm boş.
