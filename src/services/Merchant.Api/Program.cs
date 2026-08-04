using Common.Exceptions;
using Merchant.Api.ReadModels;
using Shared;
using Shared.Utils.Constants;
using Wolverine.RabbitMQ;

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

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.AddMerchantGroupEndpointExtension(apiVersionSet);
app.AddSettlementAccountGroupEndpointExtension(apiVersionSet);

await app.RunAsync();