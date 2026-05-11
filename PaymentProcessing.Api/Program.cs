using Marten;
using Marten.Events.Projections;
using Marten.Schema;
using PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Features.Endpoints;
using PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Middleware;
using PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;
using PaymentProcessing.Api.PaymentProcessing.Merchants;
using Wolverine.Marten;
using Wolverine.RabbitMQ;
using SharedPaymentEvents = PaymentGateway.SharedContracts.PaymentEvents;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

var paymentDb = builder.Configuration.GetConnectionString("paymentDb")!;
var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;

builder.Services.AddMarten(opts =>
    {
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
        opts.Schema.For<MerchantSummary>();
        opts.Schema.For<BankRouteSummary>()
            .Index(r => r.MerchantId)
            .Index(r => r.Currency);
    }).IntegrateWithWolverine(x => x.UseFastEventForwarding = true)
    .ApplyAllDatabaseChangesOnStartup();

builder.Services
    .AddGrpcClient<BankPaymentService.BankPaymentServiceClient>("garanti",
        o => o.Address = new Uri("https+http://garanti"))
    .AddServiceDiscovery();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("webhook", client => { client.Timeout = TimeSpan.FromSeconds(10); });

builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

    var transport = opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    // Subscribe to merchant data for local read model
    transport.BindExchange("merchant.created", ExchangeType.Fanout)
        .ToQueue("payment-processing.merchant-events");
    transport.BindExchange("merchant.updated", ExchangeType.Fanout)
        .ToQueue("payment-processing.merchant-events");
    transport.BindExchange("merchant.status-changed", ExchangeType.Fanout)
        .ToQueue("payment-processing.merchant-events");
    opts.ListenToRabbitQueue("payment-processing.merchant-events");

    // Subscribe to bank routing data for local read model
    transport.BindExchange("bank.route-synced", ExchangeType.Fanout)
        .ToQueue("payment-processing.bank-routes");
    opts.ListenToRabbitQueue("payment-processing.bank-routes");

    opts.PublishMessage<SharedPaymentEvents.PaymentApprovedIntegration>()
        .ToRabbitExchange("payment.approved");
    opts.PublishMessage<SharedPaymentEvents.PaymentDeclinedIntegration>()
        .ToRabbitExchange("payment.declined");
    opts.PublishMessage<SharedPaymentEvents.PaymentFailedIntegration>()
        .ToRabbitExchange("payment.failed");

    opts.Policies.AddMiddleware(typeof(MerchantMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiresMerchantAttribute>() != null);
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapPaymentTransactionEndpoints();
app.Run();