using Common.Exceptions;
using Shared;
using Shared.Utils.Constants;
using Wolverine.RabbitMQ;

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

        opts.Schema.For<Payment.Api.Domains.Payments.Payment>();
        opts.Schema.For<Payment.Api.Domains.PosAccounts.PosAccount>();
        opts.Schema.For<Payment.Api.Domains.BinCards.BinCard>()
            .Identity(x => x.BinNumber)
            .Index(x => x.CardProgram);
    })
    .IntegrateWithWolverine()
    // Katalog boşsa gömülü bincards.json'dan bir kez seed (idempotent).
    .InitializeWith(new Payment.Api.Domains.BinCards.BinCardSeeder())
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

app.AddPaymentGroupEndpointExtension(apiVersionSet);
app.AddPosAccountGroupEndpointExtension(apiVersionSet);
app.AddBinCardGroupEndpointExtension(apiVersionSet);

await app.RunAsync();