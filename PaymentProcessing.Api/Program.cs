using Hangfire;
using Hangfire.PostgreSql;
using PaymentGateway.SyncContracts.BankIntegration;
using PaymentGateway.SyncContracts.Commission;
using PaymentGateway.SyncContracts.Merchant;
using PaymentProcessing.Api.Domains.BinRecords;
using PaymentProcessing.Api.Jobs;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddKeycloakJwtAuthentication();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

var paymentDb = builder.Configuration.GetConnectionString("paymentDb")!;

builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.PAYMENT_SCHEMA_NAME;
        opts.Connection(paymentDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Projections.Snapshot<PaymentTransaction>(SnapshotLifecycle.Inline);
        opts.Schema.For<PaymentTransaction>()
            .UniqueIndex(UniqueIndexType.Computed, t => t.MerchantId, t => t.OrderId);
        opts.Schema.For<BinRecord>()
            .Index(b => b.BinEightStart)
            .Index(b => b.BinEightEnd);
        opts.Schema.For<BankCommissionSummary>();
        opts.Schema.For<MerchantCommissionSummary>();
        opts.Schema.For<MerchantBankSummary>();
        opts.Schema.For<MerchantSummary>();
    }).IntegrateWithWolverine(x => x.UseFastEventForwarding = true)
    .ApplyAllDatabaseChangesOnStartup();

builder.Services
    .AddGrpcClient<BankPaymentService.BankPaymentServiceClient>("garanti",
        o => o.Address = new Uri("https+http://garanti"))
    .AddServiceDiscovery();

builder.Services
    .AddGrpcClient<SyncBankIntegrationService.SyncBankIntegrationServiceClient>(
        o => o.Address = new Uri("https+http://bank-integration"))
    .AddServiceDiscovery();

builder.Services
    .AddGrpcClient<SyncCommissionService.SyncCommissionServiceClient>(
        o => o.Address = new Uri("https+http://commission-management"))
    .AddServiceDiscovery();

builder.Services
    .AddGrpcClient<SyncMerchantService.SyncMerchantServiceClient>(
        o => o.Address = new Uri("https+http://merchant-management"))
    .AddServiceDiscovery();

builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(paymentDb)));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<NightlyResyncJob>();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("webhook", client => { client.Timeout = TimeSpan.FromSeconds(10); });

var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    var transport = opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    transport.BindExchange(ExchangeConstants.BankCommissionSynced, ExchangeType.Fanout)
        .ToQueue("payment-processing.commission-events");

    transport.BindExchange(ExchangeConstants.MerchantCommissionSynced, ExchangeType.Fanout)
        .ToQueue("payment-processing.commission-events");

    opts.ListenToRabbitQueue("payment-processing.commission-events");

    opts.Policies.AddMiddleware(typeof(MerchantMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiresMerchantAttribute>() != null);
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire");

RecurringJob.AddOrUpdate<NightlyResyncJob>(
    "nightly-resync",
    job => job.RunAsync(),
    Cron.Daily(0));

app.Run();