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

        // 045: hosted onboarding — form oturumu + teslim linki token'la yüklenir.
        opts.Schema.For<Merchant.Api.Domains.OnboardingFormSessions.OnboardingFormSession>()
            .Index(x => x.Token)
            .Index(x => x.Email);
        opts.Schema.For<Merchant.Api.Domains.CredentialRevealLinks.CredentialRevealLink>()
            .Index(x => x.Token)
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

    // 016: deterministik mail yayını — Mail.Worker tüketip SMTP ile gönderir (MCP DEĞİL). [Transactional]
    // handler'dan PublishMessage → outbox: yalnız DB commit olursa gider, retry/dead-letter Wolverine'de.
    rabbit.DeclareExchange(RabbitMqConstants.MailDelivery.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    opts.PublishMessage<Shared.IntegrationEvents.SendEmailRequested>()
        .ToRabbitExchange(RabbitMqConstants.MailDelivery.Exchange);

    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

    // 043: tool-bazlı ince yetki — [RequiredScope] taşıyan admin MCP command/query'lerini
    // fail-closed korur (merchant.admin, dormant Common middleware'in İLK aktivasyonu).
    opts.Policies.AddMiddleware(typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
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

// 016: deterministik mailler Mail.Worker'a RabbitMQ ile publish edilir (MCP/IMailSender YOK).

// Onboarding akış ayarları (aktivasyon taban linki).
builder.Services.AddOptions<Merchant.Api.Options.Onboarding>().BindConfiguration(nameof(Merchant.Api.Options.Onboarding))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Merchant.Api.Options.Onboarding>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Merchant.Api.Options.Onboarding>>().Value);

// 043 FR-011: admin bildirim maili (submit_registration sonrası) — sabit alıcı.
builder.Services.AddOptions<Merchant.Api.Options.AdminNotification>().BindConfiguration(nameof(Merchant.Api.Options.AdminNotification))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Merchant.Api.Options.AdminNotification>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Merchant.Api.Options.AdminNotification>>().Value);

// 029: MCP server — ECommerce ChatAgent'a başvuru tool'larını sunar ([McpServerToolType]).
// Stateless HTTP (013 wiring'inin dirilişi).
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

// 023/044: merchant uçları — MerchantScoped tekil okuma + hassas-veri BFF çifti (admin CRUD
// REST 044'te söküldü, yönetim MCP'de). register-requests REST grubu da söküldü (MCP muadilleri).
app.AddMerchantGroupEndpointExtension(apiVersionSet);

// 045: store↔PG onboarding S2S REST kontratı (specs/045-hosted-onboarding-form/contracts) —
// oturum aç (write) + durum (read) + credential doğrulama (read); ecommerce-onboarding m2m.
app.MapGroup("api/v{version:apiVersion}/onboarding").WithTags("onboarding").WithApiVersionSet(apiVersionSet)
    .CreateFormSessionGroupItemEndpoint()
    .GetOnboardingApplicationStatusGroupItemEndpoint()
    .ValidateMerchantCredentialsGroupItemEndpoint();

// 045: hosted sayfalar — ANONİM, token = yetki (form ~24 saat tek başvuruluk; teslim ~1 saat
// tek gösterimlik; Payment 041 hosted sayfa emsali).
app.MapOnboardingFormPages();
app.MapCredentialRevealPage();

// 029: MCP endpoint (Streamable HTTP) — ECommerce ChatAgent buraya bağlanır. Yüzey merchant.write
// ister (ecommerce-onboarding istemcisi taşır; merchant kendi token'ı bu iç yüzeye girmez).
app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.MerchantWrite);

await app.RunAsync();