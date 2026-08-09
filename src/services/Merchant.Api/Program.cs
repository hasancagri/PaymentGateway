var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

var merchantDb = builder.Configuration.GetConnectionString("merchantDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.MerchantSchemaName;
        opts.Connection(merchantDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Schema.For<Merchant.Api.Domains.Merchants.Merchant>();
        opts.Schema.For<Merchant.Api.Domains.SettlementAccounts.SettlementAccount>();

        // Onboarding başvuru document'ı (challenge yok — descriptor + admin onayı; aktivasyon Merchant'a gömülü).
        opts.Schema.For<RegisterRequest>();

        // Reference.Api katalog verisinin yerel read-model izdüşümü (id = Code). Event ile beslenir.
        opts.Schema.For<ReferenceCountry>().Identity(x => x.Code);
        opts.Schema.For<ReferenceCity>().Identity(x => x.Code);
        opts.Schema.For<ReferenceMcc>().Identity(x => x.Code);
        opts.Schema.For<ReferenceBank>().Identity(x => x.Code);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // Reference tüketimi: fanout exchange'e bağlı durable queue'yu dinle; Handle(ReferenceDataUpdated)
    // assembly taramasıyla keşfedilir. Durable inbox → restart'ta kayıp yok, at-least-once + idempotent.
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.ReferenceDataUpdated.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    rabbit.DeclareQueue("merchant.reference-sync");
    rabbit.BindExchange(RabbitMqConstants.ReferenceDataUpdated.Exchange)
        .ToQueue("merchant.reference-sync");

    opts.ListenToRabbitQueue("merchant.reference-sync").UseDurableInbox();

    // 012: merchant yaşam döngüsü yayını — Identity.Server tüketir (OpenIddict istemci senkronu).
    rabbit.DeclareExchange(RabbitMqConstants.MerchantLifecycle.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    opts.PublishMessage<Shared.IntegrationEvents.MerchantCreated>()
        .ToRabbitExchange(RabbitMqConstants.MerchantLifecycle.Exchange);
    opts.PublishMessage<Shared.IntegrationEvents.MerchantStatusChanged>()
        .ToRabbitExchange(RabbitMqConstants.MerchantLifecycle.Exchange);
    // 013: aktivasyon (key teslim) — Identity Provisioning demetiyle client provision eder.
    opts.PublishMessage<Shared.IntegrationEvents.MerchantProvisioned>()
        .ToRabbitExchange(RabbitMqConstants.MerchantLifecycle.Exchange);

    // 013: komisyon grid-hazır tüketimi (Active koşulu #2) — Commission.Api yayınlar, durable queue
    // ile tüketilir. Handle(MerchantCommissionGridReady) tekil ...Handler assembly taramasıyla keşfedilir.
    rabbit.DeclareExchange(RabbitMqConstants.MerchantCommission.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    rabbit.DeclareQueue(RabbitMqConstants.MerchantCommission.MerchantQueue);
    rabbit.BindExchange(RabbitMqConstants.MerchantCommission.Exchange)
        .ToQueue(RabbitMqConstants.MerchantCommission.MerchantQueue);
    opts.ListenToRabbitQueue(RabbitMqConstants.MerchantCommission.MerchantQueue).UseDurableInbox();

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
    AuthorizationScopes.MerchantRead,
    AuthorizationScopes.MerchantWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 013: IMailSender (Common) — Scrutor FromApplicationDependencies bu marker'ı Common assembly'sinden
// güvenilir keşfetmiyor; açıkça kaydet (deterministik mailler: aktivasyon + admin bildirim).
builder.Services.AddSingleton<Common.Mail.IMailSender, Common.Mail.MailMcpClient>();

// 013: MCP server — Merchant.Agent'a başvuru tool'larını sunar ([McpServerToolType]). Stateless HTTP.
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

app.AddMerchantGroupEndpointExtension(apiVersionSet);
app.AddSettlementAccountGroupEndpointExtension(apiVersionSet);
app.AddRegisterRequestGroupEndpointExtension(apiVersionSet);

// 013: MCP endpoint (Streamable HTTP) — Merchant.Agent + harici LLM (get_merchant) buraya bağlanır.
// Yüzey merchant.write ister (agent + admin-ui taşır; merchant kendi token'ı bu iç yüzeye girmez).
app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.MerchantWrite);

await app.RunAsync();