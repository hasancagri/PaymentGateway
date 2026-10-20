
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
        // 088: MerchantApiKeyReference + MerchantStatusReference (X-Api-Key + statü referansı) SÖKÜLDÜ.
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
    // 088: PaymentChargedEvent publish'i + merchant.lifecycle tüketimi (statü referansı) SÖKÜLDÜ
    // (kart-vault charge + Merchant BC öldü; statü kapısı Phase-1'e devredildi).

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
// 088: cards.write + payment.charge (kart-vault/charge) SÖKÜLDÜ; X-Api-Key + MerchantApiKey/
// HostedPaymentApiKey merchant policy zinciri SÖKÜLDÜ (Merchant BC öldü). payment.* JWT scope kalır.
builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.PaymentRead,
    AuthorizationScopes.PaymentWrite);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 088: eski ödeme sağlayıcısı transport Options'ları SÖKÜLDÜ (transport engine gitti).

// 041: hosted-CF ödeme ayarları (CallbackSecret secret → user-secrets; diğerleri appsettings). Options
// pattern (BindConfiguration + Validate); handler düz POCO inject eder.
builder.Services.AddOptions<Payment.Api.Options.HostedPaymentOptions>()
    .BindConfiguration(nameof(Payment.Api.Options.HostedPaymentOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.HostedPaymentOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Payment.Api.Options.HostedPaymentOptions>>().Value);

// 047: Payment.Api MCP ucu SÖKÜLDÜ (FR-010, "PG için tek MCP"). Payment tool taşımıyordu (hosted-CF
// ödeme store HTTP çağırır, agent değil); tool'suz /mcp bırakılmaz. payment.write REST policy'leri kalır.

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// 041: hosted ödeme yüzeyi (store başlat + sağlayıcı callback + müşteri dönüş sayfası). Sürüm segmentsiz
// (dış store kontratı sabit yol — D11). Charge statü kapısı slice içinde (Active-only, fail-closed).
app.AddHostedPaymentEndpointExtension();

// 076: kart-vault + saved-card ödeme uçları SÖKÜLDÜ (card-storage teardown).
// 047: MCP endpoint SÖKÜLDÜ — Payment tool taşımıyordu (hosted-CF ödeme store HTTP çağırır, agent değil).

await app.RunAsync();