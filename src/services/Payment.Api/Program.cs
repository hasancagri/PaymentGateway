
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

        // 031: kayıtlı kart (vault) — kimlik opak Token (string; Guid Id değil), merchant-scoped index.
        opts.Schema.For<StoredCard>()
            .Identity(x => x.Token)
            .Index(x => x.MerchantId);
    })
    .IntegrateWithWolverine()
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
    // 033: kayıtlı kartla çekim tamamlandı — iyzico maliyeti taşır (komisyon tüketimi ileride).
    opts.PublishMessage<Shared.IntegrationEvents.PaymentChargedEvent>()
        .ToRabbitExchange(RabbitMqConstants.PaymentCompleted.Exchange);

    // 038: merchant.lifecycle tüketimi — statü referansı (çekim statü kapısı). Message store yok →
    // ProcessInline + RabbitMQ redelivery (Identity.Server deseni). Handle(...) tekil ...Handler
    // assembly taramasıyla keşfedilir (MerchantLifecycleEventHandler).
    rabbit.DeclareExchange(RabbitMqConstants.MerchantLifecycle.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    rabbit.DeclareQueue(RabbitMqConstants.MerchantLifecycle.PaymentQueue);
    rabbit.BindExchange(RabbitMqConstants.MerchantLifecycle.Exchange)
        .ToQueue(RabbitMqConstants.MerchantLifecycle.PaymentQueue);
    opts.ListenToRabbitQueue(RabbitMqConstants.MerchantLifecycle.PaymentQueue).ProcessInline();

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
    AuthorizationScopes.PaymentWrite,
    // 017: vault düzlemi — Active merchant token'ının kabul edildiği payment scope'u (capability).
    AuthorizationScopes.CardsWrite,
    // 033: çekim düzlemi — Active merchant charge capability.
    AuthorizationScopes.PaymentCharge);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 032: iyzico sağlayıcı ayarları — Options pattern (BindConfiguration + Validate); sandbox key
// user-secrets'tan. Handler'lar düz ProviderOptions inject eder (settings'ten map'lenmiş singleton).
builder.Services.AddOptions<Payment.Api.Options.IyzicoProviderSettings>()
    .BindConfiguration(nameof(Payment.Api.Options.IyzicoProviderSettings))
    .ValidateDataAnnotations().ValidateOnStart();
// iyzico transport ayarları (secret) → engine ProviderOptions singleton (IyzicoProviderSettings'ten map).
builder.Services.AddSingleton<Payment.Api.Utils.ProviderOptions>(sp =>
{
    var s = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Payment.Api.Options.IyzicoProviderSettings>>().Value;
    return new Payment.Api.Utils.ProviderOptions { ApiKey = s.ApiKey, SecretKey = s.SecretKey, BaseUrl = s.BaseUrl };
});
// iyzico istek sabitleri (locale/conversationId) — Options pattern, düz POCO inject.
builder.Services.AddOptions<Payment.Api.Options.IyzicoRequestOptions>()
    .BindConfiguration(nameof(Payment.Api.Options.IyzicoRequestOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.IyzicoRequestOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Payment.Api.Options.IyzicoRequestOptions>>().Value);

// 038: MCP server dirilişi (022'de sökülmüştü) — Payment.Agent'a ödeme tool'larını sunar
// ([McpServerToolType]). Stateless HTTP (Merchant.Api 029 deseni).
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

// 031: kart kasası uçları (merchants/{merchantId}/vault/cards — cards.write + MerchantScoped).
app.AddStoredCardGroupEndpointExtension(apiVersionSet);

// 033: kayıtlı kartla ödeme uçları (merchants/{merchantId}/payments — payment.charge + MerchantScoped).
app.AddPaymentGroupEndpointExtension(apiVersionSet);

// 038: MCP endpoint (Streamable HTTP) — TEK tüketici Payment.Agent (makine token'ı, payment.write).
// ChatAgent veya BC kodu buraya BAĞLANMAZ (016); çekim statü kapısı slice içinde (fail-closed).
app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.PaymentWrite);

await app.RunAsync();