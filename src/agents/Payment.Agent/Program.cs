using OpenAI;
using Payment.Agent;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// User-secrets'ı ortamdan bağımsız yükle (Aspire altında Development garanti değil; chat anahtarı
// buradan gelir).
builder.Configuration.AddUserSecrets<Program>(optional: true);

// --- Chat client (LLM) — OpenAI-uyumlu; dev'de GitHub Models / prod'da Azure OpenAI (config-driven).
// ECommerce ChatAgent deseni: OpenAI:ApiKey + OpenAI:Model (+ opsiyonel OpenAI:Endpoint).
string apiKey = builder.Configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey is not set");
string model = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini";

IChatClient chatClient = new OpenAIClient(apiKey)
    .GetChatClient(model)
    .AsIChatClient()
    .AsBuilder()
    .ConfigureOptions(o => o.ModelId = model)
    .Build();

// --- MCP tool'ları Payment.Api'den keşfet (agent = LLM router; tool'lar domain'i sarar).
using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Payment.Agent.Bootstrap");

var paymentApiBase = builder.Configuration["services:payment-api:http:0"]
                     ?? builder.Configuration["PaymentApi:BaseUrl"]
                     ?? "http://payment-api";
var mcpEndpoint = $"{paymentApiBase.TrimEnd('/')}/mcp";

var tools = await McpToolProvider.DiscoverToolsAsync(mcpEndpoint, bootstrapLogger);

// --- Router agent (ECommerce ChatClientAgent deseni): LLM sırayı kurar, domain kararları vermez.
AIAgent agent = new ChatClientAgent(
    chatClient,
    PaymentAgentPrompts.RouterInstructions,
    "PaymentAgent",
    null,
    tools);

// --- A2A server: agent'ı A2A yüzeyi olarak yayınla (Microsoft.Agents.AI.Hosting.A2A.AspNetCore).
builder.AddA2AServer(agent);

var app = builder.Build();

app.MapDefaultEndpoints();

var agentUrl = builder.Configuration["AgentCard:Url"] ?? "http://payment-agent";
var agentCard = PaymentAgentCard.Create(agentUrl);

// A2A JSON-RPC yüzeyi + Agent Card (/.well-known/agent-card.json).
app.MapA2AJsonRpc(agent, path: "/");
app.MapWellKnownAgentCard(agentCard);

await app.RunAsync();