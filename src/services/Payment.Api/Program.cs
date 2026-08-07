using Common.Exceptions;
using Common.Extensions;
using Shared;
using Shared.Utils.Constants;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

var paymentDb = builder.Configuration.GetConnectionString("paymentDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.PaymentSchemaName;
        opts.Connection(paymentDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Schema.For<Payment.Api.Domains.PosAccounts.PosAccount>();
        opts.Schema.For<Payment.Api.Domains.BinCards.BinCard>()
            .Identity(x => x.BinNumber)
            .Index(x => x.CardProgram);
        // 007 A2A: ödeme oturumu (agent-başlatımlı akışın kalıcı izdüşümü).
        opts.Schema.For<Payment.Api.Domains.PaymentSessions.PaymentSession>();
    })
    .IntegrateWithWolverine()
    // Katalog boşsa gömülü bincards.json'dan bir kez seed (idempotent).
    .InitializeWith(new Payment.Api.Domains.BinCards.BinCardSeeder())
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.PaymentCompleted.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    rabbit.DeclareExchange(RabbitMqConstants.PaymentFailed.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });

    opts.PublishMessage<Shared.IntegrationEvents.PaymentCompletedEvent>()
        .ToRabbitExchange(RabbitMqConstants.PaymentCompleted.Exchange);
    opts.PublishMessage<Shared.IntegrationEvents.PaymentFailedEvent>()
        .ToRabbitExchange(RabbitMqConstants.PaymentFailed.Exchange);

    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// 011: JWT bearer (Identity.Server JWKS) + scope policy'leri; endpoint'ler policy'yi açıkça beyan eder.
builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.PaymentRead,
    AuthorizationScopes.PaymentWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 007 A2A: MCP server — Payment.Agent'a agent tool'larını sunar (assembly'deki [McpServerToolType]).
// Stateless HTTP transport (ölçek için affinity gerektirmez). Tool gövdeleri IMessageBus ile slice sarar.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.AddPosAccountGroupEndpointExtension(apiVersionSet);
app.AddBinCardGroupEndpointExtension(apiVersionSet);

// 007 A2A: MCP endpoint (Streamable HTTP) — Payment.Agent MCP client buraya bağlanır.
// 011: yüzey tek policy ile korunur (payment.write) — tool'lar session açar/değiştirir;
// tek çağıran payment-agent zaten write taşır (ince ayrım gerekirse tool-başına policy sonraki feature).
app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.PaymentWrite);

await app.RunAsync();