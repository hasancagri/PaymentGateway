
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

var referenceDb = builder.Configuration.GetConnectionString("referenceDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.ReferenceSchemaName;
        opts.Connection(referenceDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Schema.For<Country>();
        opts.Schema.For<City>();
        opts.Schema.For<Mcc>();
        opts.Schema.For<Bank>();
    })
    .IntegrateWithWolverine()
    // Katalog boşsa gömülü JSON'dan bir kez seed (kod bazlı idempotent; büyütme destekli).
    .InitializeWith(new ReferenceSeeder())
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.ReferenceDataUpdated.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });

    opts.PublishMessage<IntegrationEvents.ReferenceDataUpdated>()
        .ToRabbitExchange(RabbitMqConstants.ReferenceDataUpdated.Exchange);

    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// Açılışta katalog tam-setini ReferenceDataUpdated ile yayınlar (HTTP yüzeyi yok — tek besleme yolu).
builder.Services.AddHostedService<ReferenceStartupPublisher>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

await app.RunAsync();