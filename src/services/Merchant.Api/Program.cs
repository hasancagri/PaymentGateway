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

        // 046: salt-append key yenileme denetim kaydı — merchantId ile geçmiş sorgusu.
        opts.Schema.For<Merchant.Api.Domains.MerchantKeyReissueLogs.MerchantKeyReissueLog>()
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
    // 046: key yenileme — Identity client_secret + Payment KeyHash güncellenir (eski key anında ölür).
    opts.PublishMessage<Shared.IntegrationEvents.MerchantKeyReissued>()
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
// 047: MCP yüzeyi ayrı authority (AgentPlatform IdP) — "Platform" şeması + Platform:<scope> policy'leri.
// REST default şeması (PG IdP) değişmez (FR-005).
builder.Services.AddPlatformMcpAuthentication(
    builder.Configuration,
    AuthorizationScopes.MerchantWrite,
    AuthorizationScopes.MerchantAdmin);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 016: deterministik mailler Mail.Worker'a RabbitMQ ile publish edilir (MCP/IMailSender YOK).

// 043 FR-011: admin bildirim maili (submit_registration sonrası) — sabit alıcı.
builder.Services.AddOptions<Merchant.Api.Options.AdminNotification>().BindConfiguration(nameof(Merchant.Api.Options.AdminNotification))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Merchant.Api.Options.AdminNotification>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Merchant.Api.Options.AdminNotification>>().Value);

// 087: store↔PG makine-handoff sırları (bootstrap register key + callback HMAC secret).
builder.Services.AddOptions<Merchant.Api.Options.OnboardingCallbackOptions>().BindConfiguration(nameof(Merchant.Api.Options.OnboardingCallbackOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Merchant.Api.Options.OnboardingCallbackOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Merchant.Api.Options.OnboardingCallbackOptions>>().Value);

// 029/047: MCP server — store fasadına (EC Mcp.Gateway) downstream. Stateless HTTP; oturum token'ının
// scope'una göre tools/list budanır (085 emsali): MerchantAdminSurface.ToolScopeMap'te olmayan tool
// herkese, olan yalnız gereken scope varsa görünür. tools/call son savunması Wolverine middleware'de.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http =>
    {
        http.Stateless = true;
        http.ConfigureSessionOptions = (ctx, opts, _) =>
        {
            var tools = opts.ToolCollection;
            if (tools is null)
                return Task.CompletedTask;
            foreach (var tool in tools
                         .Where(t => !McpScopePruningExtension.IsToolVisible(
                             t.ProtocolTool.Name, Merchant.Api.Mcp.MerchantAdminSurface.ToolScopeMap, ctx.User)).ToArray())
                tools.Remove(tool);
            return Task.CompletedTask;
        };
    })
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

// 087: store↔PG onboarding S2S REST kontratı — kayıt (write, X-Registration-Key) + durum (read) +
// credential doğrulama (read) + key yenileme (write); ecommerce-onboarding m2m. Hosted form + reveal
// sayfaları + CreateFormSession SÖKÜLDÜ (credential makine-handoff'a taşındı; insan-yüzeyi yok).
app.MapGroup("api/v{version:apiVersion}/onboarding").WithTags("onboarding").WithApiVersionSet(apiVersionSet)
    .SubmitRegistrationGroupItemEndpoint()
    .GetOnboardingApplicationStatusGroupItemEndpoint()
    .ValidateMerchantCredentialsGroupItemEndpoint()
    // 046/087: merchant self-servis key yenileme (store S2S tetik — yeni key callback'le teslim).
    .ReissueMerchantKeyGroupItemEndpoint();

// 047: MCP endpoint (Streamable HTTP) — store fasadı buraya downstream bağlanır. Mount YALNIZ
// AgentPlatform token'ını kabul eder (Platform şeması; PG IdP token'ı 401). Admin tool'ları
// merchant.admin scope'lu; mount merchant.write ister (tool budaması + [RequiredScope] ince kapı).
app.MapMcp("/mcp").RequireAuthorization(AuthenticationExtension.PlatformPolicyPrefix + AuthorizationScopes.MerchantWrite);

await app.RunAsync();