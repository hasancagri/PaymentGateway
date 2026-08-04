using Common.Exceptions;
using Commission.Api.Domains.BankCommissions;
using Commission.Api.Domains.Banks;
using Commission.Api.Domains.MerchantCommissions;
using Commission.Api.Domains.Migrations;
using Commission.Api.Domains.Reference;
using Shared;
using Shared.Utils.Constants;
using Wolverine.RabbitMQ;

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

// Açılışta bir kez: eski kart taksonomi int'lerini kanonik sete remap eder (idempotent, işaret-güdümlü).
builder.Services.AddHostedService<RemapCardTaxonomyMigration>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.AddBankGroupEndpointExtension(apiVersionSet);
app.AddBankCommissionGroupEndpointExtension(apiVersionSet);
app.AddMerchantCommissionGroupEndpointExtension(apiVersionSet);

await app.RunAsync();