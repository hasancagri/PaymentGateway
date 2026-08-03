# Phase 0 — Research: A2A Ödeme Oturumu

Tüm NEEDS CLARIFICATION çözüldü. Kararlar aşağıda; her biri Decision / Rationale / Alternatives.

## R1 — A2A taşıma katmanı (.NET)

**Decision**: `A2A` + `A2A.AspNetCore` (1.0.0-preview2) protokol tabanı; üstüne Microsoft Agent
Framework'ün **A2A hosting köprüsü** `Microsoft.Agents.AI.Hosting.A2A` + `.AspNetCore`. Agent
şu şekilde yayınlanır:

```csharp
builder.AddA2AServer(agent);                       // AIAgent → A2AServer
app.MapA2AHttpJson(agent, "/");                     // HTTP+JSON
app.MapA2AJsonRpc(agent, "/");                      // JSON-RPC
app.MapWellKnownAgentCard(agentCard);              // /.well-known/agent-card.json
```

Task yaşam döngüsü `TaskUpdater` ile: `Submit`(submitted) → `StartWork`(working) →
`RequireInput`(input-required, **taksit seçimi beklenir**) → `AddArtifact` → `Complete`/`Fail`.
Köprü içindeki `A2AAgentHandler` A2A `contextId`'yi agent session'ına eşler.

**Rationale**: Elle A2A protokolü yazmak yerine resmi köprü; `IAgentHandler` glue hazır.
İki fazlı akış (quote → input-required → pay) A2A task lifecycle'ına doğrudan oturur (spec §Mimari).

**Alternatives**: (a) Ham `A2A` SDK + elle `IAgentHandler.ExecuteAsync` — köprüyü elle yazmak,
gereksiz. (b) A2A yerine düz REST A2A-benzeri — org sınırı protokol garantisini kaybederdi.

**Caveat**: preview2 — v0.3→v1.0 breaking (`TaskManager` delegate modeli **kaldırıldı**,
`IAgentHandler`). Agent Card yolu **`/.well-known/agent-card.json`** (eski `agent.json` **değil**).
Sürüm sabitlenir. Hosting API'leri `[Experimental]` — diagnostic suppress.

## R2 — MCP (Model Context Protocol) .NET

**Decision**: Resmi C# SDK **GA 2.0.0**. Payment.Api MCP **server** açar:
`ModelContextProtocol.AspNetCore` → `AddMcpServer().WithHttpTransport(o => o.Stateless = true)
.WithTools<PaymentMcpTools>()` + `app.MapMcp()`. Tool'lar `[McpServerToolType]` /
`[McpServerTool]` + `[Description]`. Payment.Agent MCP **client**: `ModelContextProtocol.Core`
→ `McpClient.CreateAsync(new HttpClientTransport(new(){ Endpoint = http://payment-api/mcp,
TransportMode = StreamableHttp }))`, `ListToolsAsync()`.

**Kritik köprü**: `McpClientTool : AIFunction` (Microsoft.Extensions.AI) — MCP tool'ları
Agent Framework'e **adaptörsüz** `tools:` listesine girer.

**Rationale**: MCP GA (stabil); spec "içeride MCP" sözleşmesi; tool şeması LLM
function-calling'e doğal. Stateless HTTP transport ölçek için affinity gerektirmez.

**Alternatives**: stdio MCP (aynı process/child) — iki ayrı Aspire servisi olduğundan HTTP
uygun. REST + elle şema — MCP'nin sağladığı otomatik JSON Schema'yı kaybederdi.

## R3 — Microsoft Agent Framework (LLM router)

**Decision**: `Microsoft.Agents.AI` + `Microsoft.Agents.AI.OpenAI` (preview ~1.16). Chat
client'tan `AIAgent`:

```csharp
IList<AITool> tools = [.. (await mcp.ListToolsAsync()).Cast<AITool>()];
AIAgent agent = chatClient.AsAIAgent(
    instructions: "Ödeme yönlendiricisisin. Yalnız tool sırasını kur: önce get_installment_options, "
                + "kullanıcı taksit seçince select_installment. Tutar/banka/kart ÜRETME — session'dan gelir. "
                + "Çekim YOK (bu sürümde yalnız taksit seçimine kadar).",
    name: "PaymentAgent", tools: tools);
```

Framework otomatik tool-invocation döngüsü çalıştırır; LLM hangi tool'u hangi sırada
çağıracağına karar verir ama **argümanları domain'den gelir** (tutar session'da, banka router'da).

**Rationale**: FR-003 — LLM yalnız sıra kurar (quote → select), karar vermez. `RunAsync`
çok-adımlı diziyi tek turda yürütür; A2A `contextId` → session ile fazlar arası bağlam korunur.
`process_payment` tool'u 007'de **yok** (pay feature'ında eklenir).

**Alternatives**: Semantic Kernel planner — Agent Framework onun halefi, A2A+MCP köprüleri
hazır. Elle prompt+tool döngüsü — tekerleği yeniden icat.

**Caveat**: `AsAIAgent` vs `CreateAIAgent` adı preview build'ler arası değişebilir; sabitlenen
sürüme göre doğrulanır. Tümü preview — sabitlenir.

## R4 — Üçünün kompozisyonu

**Decision**: External A2A task → `A2AAgentHandler` → `AIAgent.RunAsync` → MCP client tools →
Payment.Api MCP server → `IMessageBus` → saf domain. Resmi paketlerle: `Microsoft.Agents.AI.A2A`
(A2A agent tüketimi, burada gerekmez), `.Hosting.A2A(.AspNetCore)` (agent'ı A2A server yapmak).

**Caveat**: Tek bir hazır sample üçünü birden füzelemiyor; iki resmi desenden (A2A-in host +
MCP-client-as-tools) birleştirilir — ortogonal, düz. Referans: `agent-framework`
`samples/05-end-to-end/A2AClientServer` + `Agent_Step09_UsingMcpClientAsTools`.

## R5 — Aspire chat resource + wiring

**Decision**: Dev için `Aspire.Hosting.GitHub.Models` (`AddGitHubModel("chat","openai/gpt-4o-mini")`)
— ücretsiz on-ramp; prod'da `AddAzureOpenAI(...).AddDeployment(...)`. Payment.Agent tüketir:
`builder.AddOpenAIClientFromConfiguration("chat").AddChatClient("chat")` → `IChatClient`. AppHost:
`payment-agent` projesi `WithReference(paymentApi)` (MCP için `http://payment-api`) +
`WithReference(chat)`.

**Rationale**: Aspire service discovery mevcut desen (Admin BFF gibi). GenAI telemetri Aspire
dashboard'da; hassas ödeme verisi için `OTEL...CAPTURE_MESSAGE_CONTENT` **kapalı** kalır.

**Alternatives**: Ollama (local) — offline dev; API key gerektirmez ama kalite düşük. Prod
kararı ertelenir; seam configuration-driven.

## R6 — Model A tutar düzeltmesi (FR-010/011)

**Decision**: Mevcut `GetInstallmentOptions` (query) bugün `total = amount + commission`
(Model B) hesaplıyor. Bu akış onu **kullanmaz**; yeni `QuoteInstallmentsForSession` feature'ı
Model A hesaplar: her satır kullanıcı toplamı = **sepet tutarı**, aylık = sepet tutarı / taksit.
`BankRouter` yalnız POS seçimi için (en ucuz aday) çağrılır; komisyon kullanıcı tutarına
**girmez**. Mevcut Model B query'sine **dokunulmaz** (başka tüketici olabilir; kapsam dışı).

**Rationale**: Ayrı feature, mevcut davranışı bozmadan Model A getirir; YAGNI + geri-uyum.
Taksit sayısı kümesi POS komisyon gridinden türetilir (sabit liste varsayılmaz, FR-008).

**Alternatives**: Mevcut query'yi Model A'ya çevirmek — olası başka tüketicileri kırar, kapsam
dışı risk. Mevcut query'ye `pricingModel` flag — gereksiz genelleme (YAGNI); tek model gerek.

## R7 — Token → kart bilgisi (vault seam)

**Decision**: `ICardVault { Task<Result<CardInfo>> ResolveCardInfoAsync(string token, ...); }`
Payment.Api içinde. **007'de yalnız BIN düzeyi bilgi** gerekir (banka kodu, kredi/banka,
kart programı, taksit destekleyen bankalar) — **tam PAN gerekmez** (çekim ertelendi). Şimdilik
`SimulatedCardVault` (008 vault feature'ı gelene kadar) — token → test kart BIN'i; BIN'den
`VPOSClient.CreditCardBinQuery` ile `CardInfo`. **Server-side**; A2A/LLM kanalını **geçmez**
(FR-006). Geçersiz/süresi dolmuş/yetkisiz token → Result hata, kart verisi sızmadan (FR-019).

**Rationale**: Spec dependency — vault ayrı feature; 007 yalnız "token → kart-bilgisi (BIN)"
yeteneğini tüketir. 007 PAN'a hiç dokunmadığı için güvenlik hikâyesi daha güçlü. Seam ile 008
geldiğinde impl değişir, çağıran kod aynı kalır; pay feature'ı PAN'ı aynı seam'den ekler.

**Caveat**: Tam PAN/CVV çözümü (MIT/CVV politikası) 008 + pay feature'ının konusu; 007 kapsam dışı.

**Alternatives**: Token'ı doğrudan CP.VPOS'a vermek — CP.VPOS token çözmez; seam şart. `CardData`
(PAN dahil) döndürmek — 007'de kullanılmayan veri; YAGNI, PAN'ı kapsam dışında tut.

## R8 — Test stratejisi

**Decision**: `tests/Payment.Api.Tests` (yeni, xUnit) saf domain: `PaymentSession` faz geçişleri
(açıldı → quote → seçim), seçim ⊂ sunulanlar, quote'suz oturuma select reddi, tekrarlı select
öngörülebilirliği, Model A tutar (sapma 0), boş taksit listesi → başarısız. A2A/MCP/LLM birim
testi **yok** — quickstart elle. Anayasa test kuralı.

**Rationale**: Değer domain invariant'larında; LLM/dış HTTP deterministik test edilemez.