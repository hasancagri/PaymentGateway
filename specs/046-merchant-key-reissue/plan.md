# Implementation Plan: Merchant Self-Servis MerchantKey Yenileme

**Branch**: `046-merchant-key-reissue` | **Date**: 2026-09-20 | **Spec**: [spec.md](./spec.md)

## Summary

Merchant, kaybettiği/sızdığından şüphelendiği MerchantKey yerine kimliğiyle (MerchantId) yeni key
alır. PG otorite: `Merchant.ReissueKey()` yeni `mk_` key üretir → `MerchantKeyReissued` event → Identity
client_secret + Payment.Api hash GÜNCELLENİR (eski anında ölür) → yeni key tek-kullanımlık
`CredentialRevealLink` ile bir kez gösterilir → salt-append `MerchantKeyReissueLog`. Tetik store S2S
REST (ecommerce-onboarding m2m, 045 deseni). Onboarding key-teslim yarısının yeniden kullanımı.

## Technical Context

**Language/Version**: .NET 10 (Nullable + ImplicitUsings) · **Primary Deps**: Marten (Postgres
doc/event, Newtonsoft, non-public setter+ctor) · Wolverine (in-proc + RabbitMQ fanout) · OpenIddict
(Identity.Server) · MCP SDK · **Storage**: merchantDb (Merchant.Api) · **Testing**: xUnit + Shouldly
(saf domain: `Merchant.ReissueKey`, `CredentialRevealLink` — test-first, İLKE VI) · **Project Type**:
web-service (BC) · **Constraints**: eski key HER temsilde anında geçersiz (FR-003/004); key sohbet/
log/komut'tan geçmez (FR-005).

## Constitution Check

*GATE: Phase 0 öncesi + Phase 1 sonrası.*

- **I. BC İzolasyonu**: Yenileme otoritesi Merchant.Api'de; Identity/Payment yeni `MerchantKeyReissued`
  event'iyle güncellenir (DB paylaşımı yok). Store→PG tetik = S2S REST. ✅
- **II. Zengin aggregate**: `Merchant.ReissueKey()` davranışı aggregate'te (`ResultDomain`); reveal =
  mevcut `CredentialRevealLink`. Audit = salt-append doc (aggregate değil — read-model/log istisnası). ✅
- **III. VSA+CQRS**: reissue = command slice + S2S endpoint (045 onboarding S2S grubu deseni);
  `IDocumentSession` doğrudan, repository yok. ✅
- **IV. Result**: handler `FeatureObjectResultModel`, aggregate `ResultDomain`. ✅
- **V. Kimlik/Yetki**: tetik ecommerce-onboarding m2m (mevcut); merchant key ile DEĞİL. FR-012
  rate-limit S1'e bağlı (ayrı borç, endpoint kapsanır). ✅
- **VII. FLOW.md**: Merchant.Api FLOW.md'ye reissue adımı (yeni command-event) AYNI PR'da. ✅

**Sonuç**: İhlal yok. Complexity Tracking gereksiz.

## Project Structure

### Documentation
```text
specs/046-merchant-key-reissue/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/   (reissue-s2s.md, merchant-key-reissued-event.md)
└── tasks.md     (/speckit-tasks çıktısı)
```

### Source Code (PG — gateway kapsamı)
```text
src/services/Merchant.Api/Domains/
├── Merchants/
│   ├── Merchant.cs                              # + ReissueKey() metodu
│   └── Features/Commands/ReissueMerchantKey.cs  # handler + S2S endpoint (045 deseni)
├── CredentialRevealLinks/CredentialRevealLink.cs # reuse (Create/Kill)
└── MerchantKeyReissueLogs/
    └── MerchantKeyReissueLog.cs                 # salt-append audit doc
src/others/Shared/IntegrationEvents.cs           # + MerchantKeyReissued(MerchantId, MerchantKey)
src/others/Identity.Server/EventHandlers/MerchantClientEventHandler.cs # + Handle(MerchantKeyReissued)
src/services/Payment.Api/MerchantApiConsumers.cs # + Handle(MerchantKeyReissued) → hash REPLACE
src/services/Merchant.Api/FLOW.md                # reissue adımı
```

**Structure Decision**: Mevcut Merchant.Api dizin düzeni; yeni tek klasör `MerchantKeyReissueLogs/`.
Tetik slice `Features/Commands/` (store S2S; agent MCP değil). Cross-BC güncelleme yalnız event.

## Complexity Tracking

İhlal yok — boş.
