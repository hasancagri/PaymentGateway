# Implementation Plan: Hosted-CF Ödeme Yüzeyi (iyzico Checkout Form)

**Branch**: `041-hosted-cf-payment` | **Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/041-hosted-cf-payment/spec.md`

## Summary

Store (merchant sitesi) bir sipariş için hosted ödeme başlatır; PG iyzico Checkout Form initialize
çağırır ve store'a `HostedUrl` + `PgPaymentRef` döner. Müşteri iyzico hosted sayfasında kartıyla öder;
iyzico tarayıcıyı PG'nin callback ucuna döndürür (token taşır). PG CF retrieve ile sonucu iyzico'dan
DOĞRULAR (kaynak-of-truth), girişimi terminal duruma taşır, store'un verdiği `CallbackUrl`'e **HMAC-imzalı**
sonuç bildirimi yollar (durable retry) ve müşteriye başarı/başarısız dönüş sayfası gösterir. Kart verisi
yalnız iyzico hosted sayfada. Mevcut iyzico V2 transport (`Utils/*V2`) repurpose edilir; kart-vault söküldü.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-proc bus + RabbitMQ + durable
local queues), iyzico V2 wire (`Payment.Api/Utils` — RestHttpClientV2/ProviderResourceV2/HashGeneratorV2),
ASP.NET Minimal API + API Versioning, Newtonsoft.Json (camelCase wire).

**Storage**: paymentDb (Marten). Yeni doküman: `HostedPaymentSession`. Mevcut: `MerchantStatusReference`,
`MerchantApiKeyReference` (event-fed referanslar, aggregate DEĞİL).

**Testing**: Saf domain birim testleri (`tests/Payment.Api.Tests`, xUnit). Domain-TDD: HostedPaymentSession
aggregate davranışı test-first. Handler/endpoint/wire canlı doğrulanır (quickstart, sandbox).

**Target Platform**: Linux/container; Aspire AppHost ile ayağa kalkar (Postgres + RabbitMQ). Store dış
tüketici (ECom 077).

**Project Type**: Mikroservis (tek BC — Payment.Api), server-to-server HTTP + iyzico dış entegrasyon.

**Performance Goals**: Ödeme başlatma p95 < 5 sn (SC-001, iyzico initialize gecikmesi dahil).

**Constraints**: Yalnız TL (İlke — çok-para yok, FR-012). Kart PAN/CVV hiçbir aşamada PG'ye/store'a girmez
(FR-011, SC-005). Idempotency %100 (FR-004/FR-008, SC-003). İmza doğrulanabilir %100 (FR-007, SC-004).

**Scale/Scope**: 3 user story (P1/P1/P2). 1 yeni aggregate, 2 command slice (initiate + complete/callback),
1 dış-çağrı callback ucu + dönüş sayfası, 1 durable store-bildirim mesajı, 1 imza yardımcısı, yeni Options.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden.*

| İlke | Durum | Not |
|---|---|---|
| I. BC İzolasyonu | PASS | Payment kendi DB; Merchant yalnız `MerchantStatusReference`/`MerchantApiKeyReference` (event-fed, aggregate değil). Store ile iletişim = sanksiyonlu senkron HTTP (dış tüketici). iyzico wire slice'a nested (paylaşılan SDK yok). |
| II. Zengin Aggregate | PASS | `HostedPaymentSession` zengin: private setter + `Start` factory + `MarkSucceeded`/`MarkFailed` (terminal idempotent guard). `ResultDomain` döner. Domain-TDD test-first. |
| III. VSA + CQRS | PASS | `Domains/HostedPayments/Features/Commands/{InitiateHostedPayment,CompleteHostedPayment}`. Handler doğrudan `IDocumentSession`; slice-arası yok. Minimal API endpoint-extension. |
| IV. Result Pattern | PASS | Handler `FeatureObjectResultModel<T>`; aggregate `ResultDomain`; hata `MessageItem.Code` = resource sabiti (`HostedPaymentResourceConstants`). |
| V. Kimlik + Açık Yetki | PASS | Store→PG ucu X-Api-Key şeması → `merchant_id` claim; **Active statü kapısı** (charge yalnız Active, fail-closed, FR-002). iyzico→PG callback ucu **secret-token kapılı** (C1 fix): URL path'inde per-session tahmin-edilemez `CallbackToken` = beyan-edilen yetki (varsayılan-açık DEĞİL); ikinci katman bağımsız iyzico-retrieve teyidi. Bkz. Complexity Tracking. |
| VI. Spec-Driven | PASS | Tam akış (yeni aggregate/tablo). Bu plan + tasks + implement. |
| Teknoloji/Alan | PASS | .NET 10 + Aspire + Marten + Wolverine; yalnız TRY (non-TRY reddi). CPM. Config = Options (IConfiguration doğrudan okuma yok; handler'da literal yok → IyzicoRequestOptions + yeni HostedPaymentOptions). |

**Sonuç**: Geçer. Tek gerekçeli sapma = kimliksiz callback ucu (aşağıda).

## Project Structure

### Documentation (this feature)

```text
specs/041-hosted-cf-payment/
├── plan.md              # Bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (pg-external.md, iyzico-cf-wire.md)
└── tasks.md             # /speckit-tasks (bu komutta ÜRETİLMEZ)
```

### Source Code (repository root)

```text
src/services/Payment.Api/
├── Program.cs                                   # policy + endpoint map + dönüş sayfası (düzenlenir)
├── Options/
│   ├── IyzicoRequestOptions.cs                  # CF init/retrieve path + locale/currency (mevcut, repurpose)
│   └── HostedPaymentOptions.cs                  # YENİ: CallbackSecret, IyzicoCallbackUrl, dönüş sayfası metni/URL
├── Constants/
│   └── HostedPaymentResourceConstants.cs        # YENİ: hata kodları (Payment BC sahipli)
├── Domains/HostedPayments/
│   ├── HostedPaymentSession.cs                  # YENİ aggregate (Pending → Succeeded | Failed)
│   ├── HostedPaymentEndpointExtension.cs        # YENİ: /hosted-payment + callback + dönüş sayfası map
│   └── Features/
│       ├── Commands/
│       │   ├── InitiateHostedPayment.cs         # US1: CF initialize wire (nested) + session Start
│       │   └── CompleteHostedPayment.cs         # US2/US3: CF retrieve wire (nested) + terminal + store-bildirim publish
│       └── StoreCallbackDelivery.cs             # US2/US3: durable local msg handler — HMAC imzala + POST (retry)
├── Utils/                                       # iyzico V2 transport (DEĞİŞMEZ; repurpose)
└── Domains/MerchantStatus/                      # statü + api-key referansı (mevcut; statü kapısı okunur)

tests/Payment.Api.Tests/
└── HostedPaymentSessionTests.cs                 # YENİ: aggregate davranışı (test-first)
```

**Structure Decision**: Tek servis (Payment.Api), yeni aggregate `Domains/HostedPayments/` altında VSA
dilimleriyle. iyzico wire her slice'ın içinde nested (init request → InitiateHostedPayment; retrieve →
CompleteHostedPayment). Transport engine `Utils/` tek kopya, dokunulmaz. MCP tool YOK (store HTTP çağırır,
agent değil — `/mcp` tool'suz kalır).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| `POST /internal/payments/callback/{callbackToken}` — iyzico çağırır, OAuth/X-Api-Key ekletemeyiz | iyzico hosted formu sonucu tarayıcı yönlendirmesiyle bu uca POST eder; sağlayıcı özel auth header taşıyamaz | **C1 fix**: uç KİMLİKSİZ DEĞİL — beyan-edilen yetki = URL path'indeki per-session tahmin-edilemez `CallbackToken` (PG üretir, yalnız iyzico'ya verilir; bilinmeyen → 404). (a) X-Api-Key: sağlayıcı header ekleyemez → sır URL'e gömülür (sağlayıcı aynen geri çağırır). (b) IP allowlist: CDN IP oynak, kırılgan. İkinci katman: bağımsız iyzico-retrieve teyidi + idempotent terminal (sahte gövde düşer). |
| İki callback kavramı (iyzico→PG ve PG→store) | Sözleşme gereği ayrı: iyzico dönüşü PG-sahipli sabit URL; store bildirimi store'un verdiği dinamik URL + HMAC imza | Tek URL kullanmak store ile iyzico'yu birbirine bağlar (kimlik sızması + imza modeli bozulur). |
