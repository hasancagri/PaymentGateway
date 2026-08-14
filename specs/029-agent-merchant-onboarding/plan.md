# Implementation Plan: Agent-Bazlı Merchant Onboarding Dirilişi

**Branch**: `029-agent-merchant-onboarding` | **Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/029-agent-merchant-onboarding/spec.md`

## Summary

022'de sökülen onboarding akışı 023 Merchant modeline hizalanarak diriltilir: ECommerce ChatAgent
admin personası Merchant.Api `/mcp` yüzeyindeki `submit_registration` + `registration_status`
tool'larıyla başvuru açar/sorgular; başvuru yeni `RegisterRequest` aggregate'inde Pending bekler;
gateway admini Admin UI "Merchant Talepleri" ekranından Onayla/Reddet der; onayda `Merchant.Create`
(Active) + `MerchantCreated` outbox (Identity OpenIddict senkronu) çalışır; Approved durum yanıtı
MerchantId + MerchantKey döndürür (dev-açık karar). ECommerce tarafında yalnız config + prompt alan
seti güncellenir (tool adları dış sözleşme). Kararlar: [research.md](research.md) R1-R9.

## Technical Context

**Language/Version**: C# / .NET 10 (net10.0)

**Primary Dependencies**: ASP.NET Core Minimal API, Marten (Postgres), Wolverine (+ RabbitMQ,
[Transactional] outbox), ModelContextProtocol.AspNetCore 2.0.0 (CPM pinli, Merchant.Api'de referanslı),
OpenIddict (Identity.Server — değişiklik yok), Razor Pages (Admin BFF)

**Storage**: merchantDb (Marten document store) — yeni `RegisterRequest` document'ı; migration yok
(dev, temiz başlangıç)

**Testing**: xUnit `tests/Merchant.Api.Tests` — saf domain birim testleri (RegisterRequest);
handler/HTTP/MCP entegrasyonu quickstart ile elle (bilinçli erteleme)

**Target Platform**: Aspire AppHost (macOS dev); Merchant.Api http://localhost:5202,
Identity https://localhost:5101, Admin http://localhost:5204; ECommerce ChatAgent harici tüketici

**Project Type**: Mikroservis (BC) + BFF + harici agent entegrasyonu — iki repo dokunuşu
(PaymentGateway asıl; ECommerceWithAgentFramework yalnız config+prompt)

**Performance Goals**: Yok (dev akışı; başvuru hacmi günde onlu mertebe — spec Assumptions)

**Constraints**: Tool adları `submit_registration`/`registration_status` DEĞİŞMEZ (ECommerce
allowlist); MCP yüzeyi tek policy `merchant.write`; Merchant aggregate'ine dokunulmaz;
`ecommerce-onboarding` istemcisi zaten seed'de (R3)

**Scale/Scope**: 1 yeni aggregate + 5 slice + 1 MCP tool dosyası + 2 admin ucu + 1 Admin ekranı +
ECommerce config/prompt; ~8-10 yeni dosya PaymentGateway, ~3 dosya ECommerce

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Değerlendirme | Durum |
|---|---|---|
| I. BC İzolasyonu | RegisterRequest Merchant BC içinde; başka BC'ye dokunuş yok. ECommerce ayrı SİSTEM — iletişim yalnız MCP tool sözleşmesi + OAuth (paylaşılan model/DB yok). Identity senkronu mevcut `MerchantCreated` integration event'iyle (Shared kontrat). | ✅ |
| II. Zengin Domain | `RegisterRequest` anemik değil: statik `Submit` fabrikası, `Approve`/`Reject` davranışları, invariant'lar (tip-uyum, IBAN, statü makinesi) aggregate'te. Private setter + düz enum (BaseModel/Enumeration anayasa metni bilinen bekleyen amendment — mevcut repo kuralı AggregateRoot+düz enum uygulanır). | ✅ |
| III. Vertical Slice + CQRS | 5 slice `Domains/RegisterRequests/Features/{Agents,Commands,Queries}`; static class + record + Response + Handler + endpoint-extension; `[Transactional]` mutasyonlarda; repository yok (`IDocumentSession`). Agent slice'ları Commands/Queries'e GİTMEZ (015). | ✅ |
| IV. Result Pattern | Aggregate metotları `ResultDomain`/`ResultDomain<T>` (void mutator dahil — 014); handler'lar `FeatureObjectResultModel<T>`; hata kodları resource sabitleri (`RECORD_DUPLICATE`, `INVALID_OPERATION_ERROR`, ...). | ✅ |
| V. Kimlik + Açık Yetki | Her uç policy'yi açıkça beyan eder: MCP `/mcp` → `merchant.write`; liste → `merchant.read`+`AdminPlaneOnly`; approve/reject → `merchant.write`+`AdminPlaneOnly`. `ecommerce-onboarding` mevcut istemci; scope çoğaltılmaz. MerchantKey'in statü yanıtında dönmesi anayasanın "MerchantKey yalnız connect/token'a gider" hedef modelinden bilinçli dev sapması — spec Assumptions'ta gelecek iş (redeem) olarak kayıtlı, `GetMerchantResponse.MerchantKey` (bu branch) ile aynı karar. | ⚠️ bilinçli, kayıtlı |

**Gate sonucu**: GEÇTİ (tek bilinçli sapma Complexity Tracking'de).

## Project Structure

### Documentation (this feature)

```text
specs/029-agent-merchant-onboarding/
├── plan.md              # Bu dosya
├── research.md          # R1-R9 kararları
├── data-model.md        # RegisterRequest aggregate + statü makinesi + slice haritası
├── quickstart.md        # S1-S5 canlı doğrulama
├── contracts/
│   ├── mcp-tools.md     # submit_registration + registration_status sözleşmesi
│   ├── admin-endpoints.md
│   └── ecommerce-changes.md
└── tasks.md             # /speckit-tasks üretecek (bu komut DEĞİL)
```

### Source Code (repository root)

```text
# PaymentGateway (asıl iş)
src/services/Merchant.Api/
├── Domains/RegisterRequests/                  # YENİ (data-model.md slice haritası)
│   ├── RegisterRequest.cs
│   ├── RegisterRequestStatus.cs
│   ├── RegisterRequestMcpTools.cs
│   ├── RegisterRequestEndpointExtension.cs
│   └── Features/
│       ├── Agents/{SubmitRegistrationForAgent,RegistrationStatusForAgent}.cs
│       ├── Commands/{ApproveRegisterRequest,RejectRegisterRequest}.cs
│       └── Queries/ListRegisterRequests.cs
├── Program.cs                                 # +AddMcpServer/WithToolsFromAssembly, +MapMcp("/mcp"),
│                                              #  +Schema.For<RegisterRequest>(), +endpoint group map
└── GlobalUsings.cs                            # +ModelContextProtocol using'leri (gerekirse)

src/ui/Admin/
├── Pages/RegisterRequests/Index.cshtml(.cs)   # YENİDEN doğar (023 alan seti)
├── Clients/RegisterRequestApiClient.cs        # YENİDEN doğar
├── Clients/ApiModels.cs                       # +RegisterRequest modelleri
├── Program.cs                                 # +DI kaydı
└── Pages/Shared/_Layout.cshtml                # +nav linki

tests/Merchant.Api.Tests/
└── RegisterRequestTests.cs                    # YENİ — Submit matrisi + statü makinesi

# ECommerceWithAgentFramework (yalnız config + prompt — contracts/ecommerce-changes.md)
src/agents/ChatAgent/appsettings.json          # Onboarding bölümü yeni alan seti
src/agents/ChatAgent/Program.cs                # prompt alan enjeksiyonu
src/agents/ChatAgent/Prompts.cs                # AdminOnboardingInstructions metni
```

**Structure Decision**: Mevcut BC-içi vertical slice düzeni; yeni proje YOK. ECommerce dokunuşu
davranışsız (config + yönerge metni) — o repoda build + mevcut akışın bozulmadığı S5 ile doğrulanır.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| MerchantKey `registration_status` yanıtında düz metin döner (anayasa V hedef modeli: key yalnız connect/token'a gider) | Kullanıcının açık dev kararı: "bütün süreci metin üzerinden ilerletmek", anahtar teslim sorunu şimdilik kapsam dışı; SC-003 (dosya/DB müdahalesiz uçtan uca) ancak böyle sağlanır | Redeem-link/tek-kullanımlık teslim ayrı iş olarak kayıtlı (memory + Assumptions); şimdi yapmak 029'u şişirir, ECommerce'e yeni ekran gerektirir |
