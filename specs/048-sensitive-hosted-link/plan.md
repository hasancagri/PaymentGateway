# Implementation Plan: Hassas Merchant Verisi — Hosted Link Erişimi

**Branch**: `048-sensitive-hosted-link` | **Date**: 2026-09-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/048-sensitive-hosted-link/spec.md`

## Summary

Admin hassas merchant verisini (Email/GSM/TCKN/IBAN/vergi no) LLM/agent context'ine sokmadan görüntüleyip düzenler. Agent, `merchant.admin` scope'lu MCP tool ile o merchant'a özel süreli (15dk) + tek-kullanımlık token link üretir; insan tarayıcıda açar, Merchant.Api'nin anonim token-endpoint'inde gömülü `.html` sayfayı görür/düzenler. Desen = ECommerce 078 credential-entry aynası (magic-link, token=capability). Eski `src/ui/Admin` Razor projesi sökülür.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: Marten (doküman DB + aggregate), Wolverine (IMessageBus/handler), Minimal API, `System.Security.Cryptography.RandomNumberGenerator`
**Storage**: merchantDb (Marten) — yeni `SensitiveEntrySession` dokümanı
**Testing**: Merchant.Api.Tests (xUnit); Domain-TDD `SensitiveEntrySession` için test-first
**Target Platform**: Aspire-hosted servis (Merchant.Api); hosted sayfa herhangi bir tarayıcı (mobil dahil)
**Project Type**: web-service (BC API) + gömülü hosted HTML sayfa
**Performance Goals**: token doğrulama tek doküman lookup; sayfa render statik markup + placeholder
**Constraints**: hassas veri MCP/agent context'ine ASLA girmez; token=capability; canlı test YOK (build+test doğrulama)
**Scale/Scope**: tek yeni aggregate + 1 MCP tool + 1 endpoint-extension (GET/POST) + gömülü .html; Admin projesi söküm

## Constitution Check

*GATE: Phase 0 öncesi geçmeli, Phase 1 sonrası tekrar bakılır.*

- **İLKE I (BC izolasyonu)**: ✅ `SensitiveEntrySession` merchantDb'de; veri Merchant.Api'de lokal (`bus.InvokeAsync`), S2S yok. Başka BC'ye dokunmaz.
- **İLKE II (zengin aggregate)**: ✅ `SensitiveEntrySession : AggregateRoot`; `Create`/`Consume`/`IsUsable` davranışı aggregate'te; token üretimi factory'de. Anemik değil.
- **İLKE III (VSA + CQRS)**: ✅ MCP tool `Features/Agents/Commands/RequestSensitiveLink` slice'ında (kendi handler'ı; MCP yalnız agent slice çağırır). Token-endpoint mevcut `GetMerchantSensitive`/`UpdateMerchantSensitive` handler'larını `IMessageBus` ile çağırır.
- **İLKE IV (Result)**: ✅ aggregate `ResultDomain`, handler `FeatureObjectResultModel<T>`.
- **İLKE V (scope)**: ✅ MCP tool `merchant.admin` (Wolverine scope middleware). Token-endpoint anonim ama token=capability (İlke V capability-link istisnası, ECom 078 emsali).
- **İLKE VI (Domain-TDD)**: ✅ `SensitiveEntrySession` invariant'ları test-first. Endpoint/HTML/tool test-sonra.
- **İLKE VII (FLOW.md)**: ✅ Merchant FLOW.md'ye yeni domain adımı eklenir (link üret → aç → görüntüle/düzenle → tüket).

**İhlal yok.** Gate geçti.

## Project Structure

### Documentation (this feature)

```text
specs/048-sensitive-hosted-link/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1
│   └── sensitive-hosted-link.md
└── tasks.md             # /speckit-tasks (henüz yok)
```

### Source Code (repository root)

```text
src/services/Merchant.Api/
├── Domains/Merchants/
│   ├── SensitiveEntrySession.cs                         # YENİ aggregate (: AggregateRoot)
│   ├── MerchantEndpointExtension.cs                     # DEĞİŞ: BFF sensitive GET/PUT eşlemeleri SÖKÜLÜR (handler'lar kalır)
│   └── Features/
│       ├── Agents/Commands/RequestSensitiveLink.cs      # YENİ: MCP tool + handler (merchant.admin)
│       ├── Queries/GetMerchantSensitive.cs              # KALIR (endpoint bus'tan çağırır; BFF endpoint söküm)
│       └── Commands/UpdateMerchantSensitive.cs          # KALIR (aynı)
├── SensitiveEntryEndpointExtension.cs                   # YENİ: GET/POST /merchants/sensitive/{token} (anonim, gömülü .html)
├── Pages/SensitiveEntry/*.html                          # YENİ: layout/view/success/notfound (EmbeddedResource)
├── Options/SensitiveEntryOptions.cs                     # YENİ: PublicBaseUrl + LinkLifetime
├── Program.cs                                           # DEĞİŞ: MapSensitiveEntryEndpoints + options + Discovery.IncludeType (gerekirse)
└── FLOW.md                                              # DEĞİŞ: yeni domain adımı

src/others/Payment.Identity/Config.cs                    # DEĞİŞ: ölü `admin-ui` client seed prune (RetiredClientIds)
src/ui/Admin/                                            # SÖKÜLÜR (komple)
PaymentGateway.slnx, src/aspire/AppHost/AppHost.cs       # DEĞİŞ: Admin proje referansı + AddProject SÖKÜLÜR

src/services/Merchant.Api.Tests/
└── SensitiveEntrySessionTests.cs                        # YENİ (Domain-TDD, test-first)
```

## Complexity Tracking

Anayasa ihlali yok → boş. Tek dikkat: token-endpoint anonim (capability-link) — İLKE V'in bilinçli istisnası, ECom 078 emsaliyle gerekçeli (research.md).
