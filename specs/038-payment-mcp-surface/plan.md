# Implementation Plan: Ödeme Süreci A2A + MCP Üzerinden (038)

**Branch**: `038-payment-mcp-surface` | **Date**: 2026-08-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/038-payment-mcp-surface/spec.md`

## Summary

Chat'ten ödeme akışı iki-ajanlı zincire taşınır: ECommerce ChatAgent sepeti kendi
araçlarıyla toplar, niyeti yorumlar ve yapılandırılmış ödeme isteğini **A2A** ile PG
**Payment.Agent**'a gönderir (007 dirilişi). Payment.Agent, 022'de sökülen ve bu işle geri
kurulan **Payment.Api /mcp** yüzeyindeki araçları (taksit sorgusu, kayıtlı kartla çekim)
sırayla çağırır; MCP tool'lar yalnız yeni `Features/Agents/` slice'larına gider. Gateway
kart yönetimi SUNMAZ — kart listesi/seçim ECommerce cüzdanında çözülür, A2A isteği hazır
vault token'la gelir. ECommerce'deki eski yol (Customer.Api ödeme MCP araçları + PG'ye HTTP köprüsü)
SÖKÜLÜR (Q1=A). A2A bacağı kimliksiz kalır (Q2=B, 024 ile tutarlı); /mcp bacağı makine
token'ıyla korunur; çekimde merchant statü kapısı gateway içinde fail-closed uygulanır.
İdempotency kapsam dışı (Q3=A).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Aspire (orkestrasyon), Marten (Postgres document store), Wolverine
(bus + RabbitMQ), ModelContextProtocol (MCP server/client), A2A + Microsoft Agent Framework
(preview pinler `Directory.Packages.props`'ta), OpenIddict token tüketimi (JwtBearer)

**Storage**: Postgres/Marten (Payment BC kendi DB'si). Yeni: `merchant.lifecycle`
fanout'undan beslenen merchant-statü referans dokümanı (Payment BC içinde, event-fed read
model — 010 deseni)

**Testing**: xUnit saf domain birim testleri (`tests/`); handler/HTTP/A2A entegrasyonu test
edilmez — quickstart canlı senaryolarıyla elle doğrulanır (sandbox-only kuralı)

**Target Platform**: Aspire AppHost altında koşan .NET servisleri (iki repo: PaymentGateway +
ECommerceWithAgentFramework; A2A'da ikisi aynı anda çalışır)

**Project Type**: Mikroservis (BC) + stateless agent host'ları

**Performance Goals**: Chat ödeme zinciri (A2A gidiş-dönüş + MCP + iyzico sandbox) kullanıcı
açısından makul sürede döner; özel sayısal hedef yok (sandbox)

**Constraints**: Sepet PG'ye TAŞINMAZ (kavram olarak; iyzico wire gereği çekim isteği sepet
kalem ÖZETİ taşır — aşağıda research R3). PAN/CVC/sağlayıcı token'ı MCP/A2A yanıtlarında
dönmez. Yalnız TL. Sandbox'ta test edilemeyen özellik kapsam dışı.

**Scale/Scope**: PG: 1 MCP yüzeyi (2 tool: taksit + çekim) + 2 Agent slice + Payment.Agent
dirilişi + 1 event-fed statü referansı; kartla ilgili YENİ kod yok. EC: ChatAgent A2A
bağlama + persona güncellemesi + Customer.Api ödeme MCP sökümü + kart/bağlam çözüm araçları.
İki repo, tek spec.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Değerlendirme | Durum |
|------|---------------|-------|
| I. BC İzolasyonu | Payment BC başka BC'nin DB/modeline dokunmaz. Merchant statüsü için Merchant.Api'nin DB'sine GİDİLMEZ; mevcut `merchant.lifecycle` integration event'leri (Shared kontrat, 012) Payment BC'de tüketilip yerel referans dokümanına yazılır — sanksiyonlu iletişim yolu (a). ECommerce ile iletişim yalnız A2A (agent düzlemi). | PASS |
| II. Zengin Domain | Yeni aggregate yok; mevcut `Payment`/`StoredCard` davranışları kullanılır. Merchant-statü referansı aggregate DEĞİL, event-fed read model (010 Reference deseni) — davranış taşımaz. | PASS |
| III. Vertical Slice + CQRS | Yeni işlemler `Domains/<Aggregate>/Features/Agents/` altında `<X>ForAgent` slice'ları (015 kuralı); MCP tool'lar aggregate kökünde, yalnız Agent slice çağırır, Commands/Queries'e gitmez (kod tekrarı bilinçli). Repository yok; `IDocumentSession` doğrudan. | PASS |
| IV. Result Pattern | ForAgent handler'ları `FeatureObjectResultModel<T>` döner; aggregate çağrıları `ResultDomain` sözleşmesine (014) uyar; beklenen hatalar (kart yok/Revoked, merchant Active değil, taksit yok) `MessageItem` ile taşınır. | PASS |
| V. Merkezi Kimlik | /mcp yüzeyi 011 deseniyle korunur: Payment.Agent client_credentials makine token'ı + tek policy (`payment.write`). A2A bacağı kimliksiz (Q2=B) — 024'te de böyleydi; anayasanın "her korunan uç açık yetki beyan eder" kuralı /mcp ucunda sağlanır, A2A ucu yalnız yapılandırılmış istek taşır ve ÇEKİM YETKİSİ vermez: çekim, gateway içindeki merchant-statü kapısıyla (Active değil → fail-closed RET) ayrıca korunur. Kademeli-yetki ilkesinin özü (charge yalnız Active) slice içinde korunur. PAN/CVC/token sızmaz. | PASS (gerekçeli — bkz. Complexity Tracking) |
| VI. Spec-Driven | Tam akış: spec → plan → tasks → implement. | PASS |
| Teknoloji kısıtları | CPM istisnasız; A2A/AgentFramework preview pinleri mevcut satırlarda. Yalnız TL. Aspire AppHost'tan koşulur. | PASS |

**Post-design re-check (Phase 1 sonrası)**: Tasarım artefaktları yukarıdaki değerlendirmeyi
değiştirmedi; ihlal yok. Tek gerekçeli sapma Complexity Tracking'de.

## Project Structure

### Documentation (this feature)

```text
specs/038-payment-mcp-surface/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md        # Phase 1 çıktısı
├── quickstart.md        # Phase 1 çıktısı
├── contracts/
│   ├── a2a-payment-agent.md   # Agent Card + A2A mesaj sözleşmesi
│   └── mcp-payment-tools.md   # /mcp tool sözleşmeleri
└── tasks.md             # Phase 2 (/speckit-tasks üretir)
```

### Source Code (repository root)

```text
# PaymentGateway reposu
src/services/Payment.Api/
├── Domains/Payments/
│   ├── PaymentMcpTools.cs                      # YENİ — MCP tool'lar (aggregate kökü)
│   └── Features/Agents/
│       ├── InstallmentOptionsForAgent.cs       # YENİ — taksit sorgusu (Agent slice)
│       └── ChargeSavedCardForAgent.cs          # YENİ — çekim (Agent slice, statü kapılı)
├── Domains/StoredCards/                        # DEĞİŞMEZ — kartla ilgili yeni kod YOK
├── Domains/MerchantStatus/                     # YENİ — event-fed statü referansı (010 deseni)
│   ├── MerchantStatusReference.cs              # doküman (aggregate değil)
│   └── MerchantLifecycleEventHandler.cs        # Wolverine tüketici (TEKİL "Handler"!)
└── Program.cs                                  # /mcp map + payment.write policy + RabbitMQ dinleme

src/agents/Payment.Agent/
├── ConstValues.cs                              # RouterInstructions genişler (çekim eklenir)
├── PaymentAgentCard.cs                         # Yeni skill: charge_saved_card
├── McpToolProvider.cs                          # Payment.Api /mcp'ye bağlanır (canlanır)
└── Program.cs                                  # A2A host (mevcut iskelet)

# ECommerceWithAgentFramework reposu
src/agents/ChatAgent/
├── ConstValues.cs                              # A2A skill sabitleri güncellenir; ödeme MCP tool sabitleri SÖKÜLÜR
├── Program.cs                                  # assistant persona: ödeme = A2A; Customer ödeme tool kayıtları çıkar
└── (A2A named-client altyapısı — mevcut, yeniden kullanılır)

src/services/customer/Customer.Api/Domains/Wallets/
├── SavedCardPaymentMcpTools.cs                 # SÖKÜLÜR (get_card_installments, charge_default_card)
├── Features/Agents/GetCardInstallments.cs      # SÖKÜLÜR
├── Features/Agents/ChargeDefaultCard.cs        # SÖKÜLÜR (PG'ye HTTP köprüsü dahil)
└── Features/Agents/GetPaymentContextForAgent.cs # YENİ — çekim bağlamı (kart referansı + buyer + sepet özeti)

tests/ (PaymentGateway)
└── Payment.Api.Tests (varsa) / Merchant.Api.Tests — saf domain testleri; yeni statü-kapısı
    değerlendiricisi + maskeleme saf fonksiyonları test edilir
```

**Structure Decision**: Mevcut BC/agent yerleşimi korunur; yeni kod yalnız yukarıdaki
dosyalara girer. Payment BC'de `Domains/MerchantStatus/` klasörü aggregate içermez (015
istisna listesi: event-fed referans + event handler aggregate kökünde durabilir deseni, 010
Reference.Api emsali).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| A2A bacağı kimliksiz (İlke V'in "her uç açık yetki" hedefine kısmi istisna) | Kullanıcı kararı Q2=B; 024 emsali (A2A auth ertelenmiş, ayrı auth işine ait). Çekim yetkisi yine de açıkta değil: /mcp makine token'ı + gateway-içi Active-merchant fail-closed kapısı | A2A'ya merchant token taşımak auth işini bu spec'e katlardı (merchantKey dağıtımı, token yenileme, A2A header sözleşmesi); kullanıcı erteledi — anayasadaki açık TODO(AUTHZ_MODEL) kapsamındaki ertelenmiş auth işinde kapanacak (o iş açıldığında A2A bacağı oraya bağlanır) |
| Payment BC'de merchant-statü referansı (yeni event tüketimi) | FR-009 statü kapısı /mcp bacağında token'la sağlanamaz (makine token'ı statü taşımaz); fail-closed kapı için Payment BC'nin statüyü YEREL bilmesi gerekir | Merchant.Api'ye BC→BC HTTP sorgusu her çekimde çapraz-BC senkron bağımlılık yaratır (I. ilkenin ruhuna aykırı sıkı bağ); event-fed read model 010/012 emsalleriyle yerleşik desen |