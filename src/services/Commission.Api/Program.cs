
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

    // 013/019: komisyon hazır → Merchant.Api tüketir (Active koşulu #2). Fanout exchange; event state
    // değişikliğiyle aynı [Transactional] commit'te outbox'a yazılır (dual-write yok — D13).
    // 019'da kaynak teklif kabul handler'ıdır (AcceptCommissionProposal).
    rabbit.DeclareExchange(RabbitMqConstants.MerchantCommission.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    opts.PublishMessage<Shared.IntegrationEvents.MerchantCommissionGridReady>()
        .ToRabbitExchange(RabbitMqConstants.MerchantCommission.Exchange);

    // 019: teklif maili (Excel tablolu) → Mail.Worker tüketir. Outbox: yalnız DB commit'te gider.
    rabbit.DeclareExchange(RabbitMqConstants.MailDelivery.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    opts.PublishMessage<Shared.IntegrationEvents.SendEmailRequested>()
        .ToRabbitExchange(RabbitMqConstants.MailDelivery.Exchange);

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

// 019: teklif ayarları (marj + bilet TTL + public link tabanı) — strongly-typed POCO.
builder.Services.AddOptionsExt();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// 024: Commission BC gerçek domain — marj politikası + efektif komisyon uçları.
app.AddCommissionPolicyGroupEndpointExtension(apiVersionSet);

await app.RunAsync();