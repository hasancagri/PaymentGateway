
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

var commissionDb = builder.Configuration.GetConnectionString("commissionDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.CommissionSchemaName;
        opts.Connection(commissionDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Schema.For<BankCommission>();
        opts.Schema.For<MerchantCommission>();
        opts.Schema.For<Bank>();

        // 013: merchant grid başlık kaydı (Draft/Ready) — kimlik = MerchantId.
        opts.Schema.For<Commission.Api.Domains.MerchantCommissions.MerchantCommissionGrid>()
            .Identity(x => x.MerchantId);

        // Reference.Api banka kataloğunun yerel read-model izdüşümü (id = Code). Event ile beslenir.
        opts.Schema.For<ReferenceBank>().Identity(x => x.Code);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // Reference tüketimi: fanout exchange'e bağlı durable queue; Handle(ReferenceDataUpdated) yalnız
    // Kind=="Bank" ile ilgilenir. Durable inbox → restart dayanıklı, at-least-once + idempotent upsert.
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.ReferenceDataUpdated.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    rabbit.DeclareQueue("commission.reference-sync");
    rabbit.BindExchange(RabbitMqConstants.ReferenceDataUpdated.Exchange)
        .ToQueue("commission.reference-sync");

    opts.ListenToRabbitQueue("commission.reference-sync").UseDurableInbox();

    // 013: grid finalize → Merchant.Api tüketir (Active koşulu #2). Fanout exchange; event state
    // değişikliğiyle aynı [Transactional] commit'te outbox'a yazılır (dual-write yok — D13).
    rabbit.DeclareExchange(RabbitMqConstants.MerchantCommission.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    opts.PublishMessage<Shared.IntegrationEvents.MerchantCommissionGridReady>()
        .ToRabbitExchange(RabbitMqConstants.MerchantCommission.Exchange);

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
    AuthorizationScopes.CommissionRead,
    AuthorizationScopes.CommissionWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 013 US4: MCP server — komisyon Excel orkestrasyonu için get_merchant_commission_grid ([McpServerToolType]).
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

app.AddBankGroupEndpointExtension(apiVersionSet);
app.AddBankCommissionGroupEndpointExtension(apiVersionSet);
app.AddMerchantCommissionGroupEndpointExtension(apiVersionSet);

// 013 US4: MCP endpoint (harici LLM/MCP client, commission.read).
app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.CommissionRead);

await app.RunAsync();