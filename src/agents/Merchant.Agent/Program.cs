using Merchant.Agent;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// User-secrets'ı ortamdan bağımsız yükle (Aspire altında Development garanti değil; chat anahtarı buradan).
builder.Configuration.AddUserSecrets<Program>(optional: true);

// --- Chat client (LLM) — OpenAI-uyumlu (Payment.Agent deseni).
string apiKey = builder.Configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey is not set");
string model = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini";

IChatClient chatClient = new OpenAIClient(apiKey)
    .GetChatClient(model)
    .AsIChatClient()
    .AsBuilder()
    .ConfigureOptions(o => o.ModelId = model)
    .Build();

// --- MCP tool'ları Merchant.Api'den keşfet (agent = LLM router; tool'lar domain'i sarar).
using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Merchant.Agent.Bootstrap");

var merchantApiBase = builder.Configuration["services:merchant-api:http:0"]
                      ?? builder.Configuration["MerchantApi:BaseUrl"]
                      ?? "http://merchant-api";
var mcpEndpoint = $"{merchantApiBase.TrimEnd('/')}/mcp";

// /mcp merchant.write ister — MCP trafiği AgentTokenHandler'lı HttpClient'la Bearer taşır.
// Client tool çağrıları boyunca yaşamalı → dispose edilmez (McpToolProvider.KeepAlive ile aynı ömür).
var mcpHttpClient = new HttpClient(new AgentTokenHandler(builder.Configuration)
{
    InnerHandler = new HttpClientHandler()
});

var tools = await McpToolProvider.DiscoverToolsAsync(mcpEndpoint, mcpHttpClient, bootstrapLogger);

// --- Router agent: LLM sırayı kurar, domain kararları vermez.
AIAgent agent = new ChatClientAgent(
    chatClient,
    ConstValues.RouterInstructions,
    "MerchantAgent",
    null,
    tools);

// --- A2A server: agent'ı A2A yüzeyi olarak yayınla.
builder.AddA2AServer(agent);

var app = builder.Build();

app.MapDefaultEndpoints();

var agentUrl = builder.Configuration["AgentCard:Url"] ?? "http://merchant-agent";
var agentCard = MerchantAgentCard.Create(agentUrl);

// A2A JSON-RPC yüzeyi + Agent Card (/.well-known/agent-card.json).
app.MapA2AJsonRpc(agent, path: "/");
app.MapWellKnownAgentCard(agentCard);

await app.RunAsync();