
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

        // 076: StoredCard + Payment + CardSession şemaları SÖKÜLDÜ (kart-vault + charge kaldırıldı).
        // X-Api-Key auth lookup — merchant API key hash'i (kiracı-içi tekil). KALIR (MerchantStatus).
        opts.Schema.For<MerchantApiKeyReference>()
            .Index(x => x.KeyHash, idx => idx.IsUnique = true);

        // 041: hosted-CF ödeme girişimi. (MerchantId, OrderRef) tekil (FR-004 idempotent başlatma);
        // CallbackToken tekil (C1 — kimliksiz callback ucunun secret-token lookup'ı + beyan-edilen yetki).
        opts.Schema.For<Payment.Api.Domains.HostedPayments.HostedPaymentSession>()
            .Index(x => new { x.MerchantId, x.OrderRef }, idx => idx.IsUnique = true)
            .Index(x => x.CallbackToken, idx => idx.IsUnique = true);
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

// 039: yapısal çekim/retrieve X-Api-Key şeması — JWT'nin YANINA eklenir (additive). Header yoksa
// NoResult → JWT şeması denenir. Merchant key SHA-256 → MerchantApiKeyReference lookup → merchant_id claim.
builder.Services.AddAuthentication()
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, null);
// Policy: ApiKey şeması + mevcut MerchantScopeRequirement (claim == route {merchantId}).
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.MerchantApiKey, policy =>
    {
        policy.AuthenticationSchemes.Add(ApiKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new Common.Utils.Authorization.MerchantScopeRequirement());
    })
    // 041 (C1/D1): hosted ödeme başlatma ucu. X-Api-Key şeması + authenticated; route'ta {merchantId}
    // YOK → tenant merchant_id claim'inden okunur (MerchantScopeRequirement KULLANILMAZ — route'suz
    // fail-closed RET ederdi). Active statü kapısı slice içinde (charge yalnız Active, İlke V).
    .AddPolicy(HostedPaymentPolicies.HostedPaymentApiKey, policy =>
    {
        policy.AuthenticationSchemes.Add(ApiKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
    });

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

// 041: hosted-CF ödeme ayarları (CallbackSecret secret → user-secrets; diğerleri appsettings). Options
// pattern (BindConfiguration + Validate); handler düz POCO inject eder.
builder.Services.AddOptions<Payment.Api.Options.HostedPaymentOptions>()
    .BindConfiguration(nameof(Payment.Api.Options.HostedPaymentOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.HostedPaymentOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Payment.Api.Options.HostedPaymentOptions>>().Value);

// 038: MCP server dirilişi (022'de sökülmüştü) — dış MCP istemcisine (BYO-agent) ödeme tool'larını
// sunar ([McpServerToolType]). Stateless HTTP (Merchant.Api 029 deseni).
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

// 041: hosted-CF ödeme yüzeyi (store başlat + iyzico callback + müşteri dönüş sayfası). Sürüm segmentsiz
// (dış store kontratı sabit yol — D11). Charge statü kapısı slice içinde (Active-only, fail-closed).
app.AddHostedPaymentEndpointExtension();

// 076: kart-vault + saved-card ödeme uçları SÖKÜLDÜ (card-storage teardown). MCP endpoint kalır (hosted-CF
// ödeme store HTTP çağırır, agent değil → tool'suz durur; İlke: MCP yalnız agent yüzeyi).
app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.PaymentWrite);

await app.RunAsync();