# Implementation Plan: MerchantId Görünürlük Politikası (PG bacağı)

**Branch**: `087-merchant-id-visibility` | **Date**: 2026-10-05 | **Spec**: [spec.md](./spec.md)

**Input**: `specs/087-merchant-id-visibility/spec.md`

## Summary

İki temel direk: **(A) merchant sürecinde hiçbir HTML ekran sır/PII render etmez**, **(B) MCP/REST tool dönüşü hassas veri (credential + finansal/PII) içermez**. PG tarafında credential teslimi reveal-sayfasından **makine-handoff'a** (store'a HMAC-imzalı dayanıklı callback) taşınır; kayıt store-başlatan S2S ucuyla gelir (bootstrap key'le yetkili); credential-sızdıran eski yüzeyler (reveal, hosted form, resend-link) sökülür; tool dönüşleri sır/PII-free nöbetçiyle garanti edilir. Callback gönderici 041 `StoreCallbackDelivery` desenini aynalar (Wolverine durable outbox + hex HMAC-SHA256). Store bacağı (ECommerceAgent) bitti; callback alıcısı + gRPC kullanım hazır.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings)
**Primary Dependencies**: Marten (merchantDb document store), Wolverine (in-proc bus + RabbitMQ fanout + `[Transactional]` durable outbox callback teslimi), ASP.NET Minimal API (register endpoint), OpenIddict relying-party (m2m `ecommerce-onboarding` + platform token MCP), `System.Security.Cryptography.HMACSHA256`
**Storage**: merchantDb — `RegisterRequest` (EXTEND: CorrelationId + CallbackUrl), `Merchant` (değişmez — callbackUrl RegisterRequest'ten çözülür)
**Testing**: xUnit — `RegisterRequest` correlation/callback davranışı test-first (İlke VI); callback HMAC imza birim testi
**Target Platform**: Linux server, Aspire AppHost
**Project Type**: web-service (tek BC mikroservis — Merchant.Api)
**Performance Goals**: N/A (düşük hacim; tek first-party merchant/store bugün)
**Constraints**: Callback dayanıklı + retry + idempotent; HMAC imzalanır; MerchantKey/iban hiçbir log/trace/tool-dönüşünde
**Scale/Scope**: Tek store (ECommerce) bugün; çoklu-site YAGNI

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden.*

- **İlke I (BC izolasyonu):** store→PG register = dış PSP S2S (HTTP, sanksiyonlu); PG→store callback = dış-webhook (sanksiyonlu istisna, CLAUDE.md S2S kuralı). Kontratlar `contracts/` altında bilinçli sözleşme. DB paylaşımı yok. ✓
- **İlke II (zengin aggregate):** `RegisterRequest` correlation/callbackUrl davranışla taşır (anemik değil); `Merchant` dokunulmaz. ✓
- **İlke III (VSA+CQRS, MCP ince):** register = `Features/Commands/SubmitRegistration` yazma slice (`[Transactional]`); approve = mevcut `Features/Agents/Commands/AdminApproveRegistration` (ince MCP sarmalayıcı) EXTEND; callback teslimi = `Features/Commands` slice (041 aynası, Wolverine durable). Repository yok; Minimal API. ✓
- **İlke IV (Result):** tüm handler/aggregate `FeatureResultModel`/`ResultDomain`; hata kodu resource sabiti. ✓
- **İlke V (kimlik + açık yetki):** register REST ucu PG Identity.Server m2m token (`ecommerce-onboarding`, `merchant.write` scope) + ayrı `X-Registration-Key` (bootstrap, dar kapsam); approve MCP tool platform token (`merchant.admin`); callback outbound (inbound auth yok, HMAC wire-imza = kanıt). Hassas-veri döndüren hiçbir uç varsayılan-açık değil; scrub FR-B. Yüzey-başına-tek-otorite (047) korunur. ✓
- **İlke VI (Domain-TDD):** `RegisterRequest.Submit` (+correlation+callbackUrl) + idempotency davranışı test-first. ✓
- **İlke VII (FLOW.md):** Merchant.Api onboarding domain süreci değişiyor (reveal→makine-callback, form→register, resend söküm) → `src/services/Merchant.Api/FLOW.md` aynı PR'da güncellenir; `check-flow-links.sh` yeşil. ✓

**Gate: PASS** — ihlal yok, Complexity Tracking gereksiz.

## Project Structure

### Documentation (this feature)

```text
specs/087-merchant-id-visibility/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (register-endpoint + credential-callback, PG tarafı)
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (PaymentGateway, Merchant.Api)

```text
src/services/Merchant.Api/
├── Domains/RegisterRequests/
│   ├── RegisterRequest.cs                              # EXTEND: CorrelationId + CallbackUrl (Submit taşır)
│   └── Features/
│       ├── Commands/
│       │   └── SubmitRegistration.cs                   # YENİ: store-başlatan S2S register slice + endpoint
│       └── Agents/Commands/
│           ├── AdminApproveRegistration.cs             # EXTEND: reveal+email yerine credential callback publish
│           └── AdminResendCredentialLink.cs            # TEARDOWN (reveal-link tabanlı)
│       └── Queries/GetOnboardingApplicationStatus.cs   # KEEP (red'i store buradan öğrenir)
├── Domains/Merchants/
│   ├── Merchant.cs                                     # KEEP (değişmez)
│   └── Features/
│       ├── Commands/ReissueMerchantKey.cs              # EXTEND: RevealUrl yerine callback publish
│       ├── Commands/DeliverCredentialCallback.cs       # YENİ: 041 StoreCallbackDelivery aynası (durable + HMAC)
│       └── Queries/GetMerchant.cs                      # EXTEND: MerchantKey dönüşten çıkar (scrub, FR-B2)
├── Domains/CredentialRevealLinks/                      # TEARDOWN (tüm klasör: endpoint+aggregate+RevealCredentials)
├── Domains/OnboardingFormSessions/                     # TEARDOWN (tüm klasör: endpoint+aggregate+CreateFormSession+form HTML)
├── Options/OnboardingCallbackOptions.cs                # YENİ: BootstrapRegistrationKey + CallbackSecret
├── Infrastructure/CallbackSignatureGenerator.cs        # YENİ (gerekirse): HMAC-SHA256 hex (041 util aynası)
├── Mcp/MerchantAdminSurface.cs                         # EXTEND: resend tool haritadan çıkar; register-yolu MCP değil
└── Program.cs                                          # register endpoint map; reveal/form map söküm; options kaydı; Wolverine IncludeType (gerekirse)
```

**Structure Decision**: Mevcut VSA düzeni korunur. Register S2S ucu `RegisterRequests/Features/Commands/SubmitRegistration` (store = dış tetikleyici, meşru yazma-niyet yüzeyi; eski form-submit'in yerine). Callback teslimi `Merchants/Features/Commands/DeliverCredentialCallback` — 041 emsali (approve `[Transactional]` handler'dan `IMessageBus.PublishAsync`, Wolverine durable queue retry). CallbackUrl reissue için `RegisterRequest`'ten (MerchantId ile) çözülür — `Merchant` aggregate'i onboarding-transport kaygısıyla kirletilmez. Wolverine callback handler adı `*Handler` değilse `Program.cs`'e `IncludeType` (keşif tuzağı).

## Complexity Tracking

Gereksiz — Constitution Check PASS, ihlal yok.