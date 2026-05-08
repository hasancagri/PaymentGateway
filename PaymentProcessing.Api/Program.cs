using Marten;
using Marten.Events.Projections;
using Marten.Schema;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PaymentProcessing.Api.Modules.PaymentProcessing.BinRecords;
using PaymentProcessing.Api.Modules.PaymentProcessing.Merchants;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Features.Endpoints;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Middleware;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ReadModels;
using Wolverine;
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
var jwtSecret = builder.Configuration["Jwt__SecretKey"]!;

// JWT validation — trusts tokens issued by ApiGateway
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = "payment-gateway",
            ValidateAudience = true,
            ValidAudience = "payment-gateway-services",
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddMarten(opts =>
{
    opts.Connection(paymentDb);
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
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapPaymentTransactionEndpoints();
app.Run();
