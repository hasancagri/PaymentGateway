# Implementation Plan: Onboarding Aggregate Sadeleştirme (5 → 2)

**Branch**: `015-onboarding-aggregate-slim` | **Date**: 2026-08-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/015-onboarding-aggregate-slim/spec.md`

## Summary

013 merchant onboarding beş aggregate'e dağılmıştır. Bu feature süreci **iki aggregate**'e
indirir: `DomainControlChallenge` → `RegisterRequest`'e gömülür (talep artık challenge'dan önce
`AwaitingDomainControl` statüsünde doğar), `ActivationTicket` → `Merchant`'a gömülür (key + süre +
kullanıldı; `IssueActivation`/`RedeemActivation` metotları), `OnboardingNotification` silinir (mail
`ILogger` + Mail.Mcp ile korunur, ayrı durum kaydı yok). Dış sözleşme yalnız EKLEMELİ değişir:
`submit_registration` `AwaitingDomainControl`'de artık `RequestId` döner; `registration_status`
yeni durumu `Message` metniyle raporlar (on-demand, poll zorunlu değil). Davranış korunur;
`Merchant.TryActivate()` 3-koşul kapısı + komisyon-grid fanout değişmez. Dev aşaması: migration
yok, DB sıfırlanabilir.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-proc bus + RabbitMQ
fanout), ModelContextProtocol (MCP tool yüzeyi), Common (Result pattern, IMailSender), Aspire

**Storage**: Marten/Postgres — `RegisterRequest` ve `Merchant` document'ları. Silinen üç
aggregate'in document tipleri kaldırılır (dev: DB sıfırlanabilir, migration yok).

**Testing**: Saf domain birim testleri (`tests/Merchant.Api.Tests`) — xUnit. Challenge +
aktivasyon davranışları yeni sahiplerinde (RegisterRequest / Merchant) test edilir.

**Target Platform**: Linux/dev; Aspire AppHost üzerinden ayağa kalkar (Postgres + RabbitMQ +
Identity.Server + Mail.Mcp/Mailpit).

**Project Type**: Mikroservis BC (Merchant.Api) — mevcut yapıya cerrahi refactor.

**Performance Goals**: Yok (davranış-korumalı yapısal refactor; hedef metrik değil).

**Constraints**: Dış MCP/HTTP sözleşmeleri yalnız eklemeli değişebilir (FR-013); yanıt `Status`
string'leri (`"ChallengeRequired"`/`"Pending"`) sabit kalır; `MerchantProvisioned` event sözleşmesi
korunur; yalnız TL (alan-dışı).

**Scale/Scope**: Tek BC (Merchant.Api). 3 aggregate + ilgili Features/MCP/endpoint dosyaları
silinir/taşınır; 2 aggregate genişler.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

- **I. BC İzolasyonu** ✅ Değişim tümüyle Merchant.Api içinde; başka BC'nin DB/aggregate'ine
  dokunulmaz. `MerchantProvisioned`/`merchant.commission` event sözleşmeleri korunur.
- **II. Zengin Domain + Invariant** ✅ Challenge (token/verify) ve aktivasyon (tek-kullanım/TTL)
  invariant'ları handler'a sızmaz; yeni sahibi aggregate metotlarında yaşar (`RegisterRequest`
  challenge davranışı, `Merchant.RedeemActivation`). Anemikleşme YOK — davranış aggregate'lere toplanır.
- **III. Vertical Slice + CQRS** ✅ Feature slice deseni korunur; silinen aggregate'lerin
  slice'ları kalan iki aggregate'in slice'larına taşınır (ör. `RedeemActivation` →
  `Merchants/Features/Commands`). Repository yok, `IDocumentSession` doğrudan.
- **IV. Result Pattern** ✅ Taşınan davranışlar `ResultDomain`/`ResultDomain<T>` döner (014
  standardı); `MessageItem.Code` resource sabiti kalır.
- **V. Merkezi Kimlik + Kademeli Yetki** ✅ Aktivasyon key'i Merchant'a taşınsa da redeem ucu
  `AdminPlaneOnly` + `merchant.write` kalır; `MerchantProvisioned` (Provisioning demeti) event'i
  değişmez; statü-yetki (013) etkilenmez.
- **VI. Spec-Driven** ✅ Bu akış spec → clarify → plan → tasks → implement izler.

**Aggregate-klasör kuralı (CLAUDE.md)** ✅ Silme sonrası `Domains/` altında yalnız
`RegisterRequests/` + `Merchants/` (+ mevcut `MerchantSettlementAccounts/`) kalır; her klasör tek
`: AggregateRoot`. Challenge/aktivasyon enum'ları ilgili aggregate klasörü altında durur;
`ChallengeOutcome` `RegisterRequests/`'e taşınır.

**Sonuç**: Gate PASS — ihlal yok, Complexity Tracking gereksiz. (Refactor karmaşıklığı DÜŞÜRÜR.)

## Project Structure

### Documentation (this feature)

```text
specs/015-onboarding-aggregate-slim/
├── plan.md              # Bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (dış sözleşmeler — eklemeli değişim)
└── tasks.md             # /speckit-tasks (bu komut ÜRETMEZ)
```

### Source Code (repository root)

Değişim `src/services/Merchant.Api` içinde yoğunlaşır:

```text
src/services/Merchant.Api/Domains/
├── RegisterRequests/                      # GENİŞLER (challenge gömülür)
│   ├── RegisterRequest.cs                 # + challenge alanları/metotları; Status += AwaitingDomainControl
│   ├── ChallengeOutcome.cs                # DomainControlChallenges'tan TAŞINIR (enum)
│   ├── ValueObjects/MerchantDescriptor.cs # aynen
│   ├── RegisterRequestEndpointExtension.cs
│   └── Features/
│       ├── Agent/SubmitRegistration.cs    # incelir (challenge artık request üstünde)
│       ├── Agent/RegistrationStatus.cs    # + Message metni, AwaitingDomainControl raporlar
│       ├── Commands/ApproveRegisterRequest.cs   # ticket üretimi → Merchant.IssueActivation
│       └── Commands/RejectRegisterRequest.cs
├── Merchants/                             # GENİŞLER (aktivasyon bileti gömülür)
│   ├── Merchant.cs                        # + Activation alanları; IssueActivation/RedeemActivation
│   └── Features/Commands/RedeemActivation.cs   # ActivationTickets'tan TAŞINIR
├── MerchantSettlementAccounts/           # değişmez
├── DomainControlChallenges/              # SİLİNİR (tüm klasör)
├── ActivationTickets/                    # SİLİNİR (tüm klasör)
└── OnboardingNotifications/              # SİLİNİR (tüm klasör)

src/services/Merchant.Api/McpTools/MerchantOnboardingMcpTools.cs  # açıklama metni güncellenir
src/services/Merchant.Api/ReadModels/MerchantCommissionGridReadyHandler.cs  # değişmez
tests/Merchant.Api.Tests/                 # challenge + aktivasyon testleri yeni sahiplerine taşınır
```

**Structure Decision**: Mevcut Vertical Slice + aggregate-klasör yapısı korunur. Üç aggregate
klasörü tümüyle silinir; davranışları iki kalan aggregate'in klasörüne (kök dosya + Features
slice) taşınır. Yeni proje/katman EKLENMEZ.

## Complexity Tracking

Yok — Constitution Check ihlalsiz geçti (refactor net karmaşıklık azaltır).