using Marten;
using Marten.Events.Projections;
using Marten.Schema;
using PaymentGatewayApi.Auths;
using PaymentGatewayApi.Modules.BankIntegration.Banks.Features.Endpoints;
using PaymentGatewayApi.Modules.PaymentProcessing.BinRecords.Features.Endpoints;
using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Features.Endpoints;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Features.Endpoints;
using PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Features.Endpoints;
using PaymentGatewayApi.Modules.IAM.Roles.Features.Endpoints;
using PaymentGatewayApi.Modules.IAM.Users.Features.Endpoints;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Endpoints;
using PaymentGatewayApi.Modules.PaymentProcessing.BinRecords;
using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions;
using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Endpoints;
using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Middleware;
using PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Endpoints;
using PaymentGatewayApi.Modules.Settlement.Settlements.Features.Endpoints;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .Build();
builder.Services.AddSingleton(config);

builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Global exception handling
builder.Services.AddGlobalExceptionHandler();

// DbContext
var defaultDb = builder.Configuration.GetConnectionString("defaultDb");

builder.Services.AddDbContextWithWolverineIntegration<BankIntegrationContext>(x => x.UseNpgsql(defaultDb));
builder.Services.AddDbContextWithWolverineIntegration<CommissionManagementContext>(x => x.UseNpgsql(defaultDb));
builder.Services.AddDbContextWithWolverineIntegration<IamContext>(x => x.UseNpgsql(defaultDb));
builder.Services.AddDbContextWithWolverineIntegration<MerchantManagementContext>(x => x.UseNpgsql(defaultDb));
builder.Services.AddDbContextWithWolverineIntegration<SettlementContext>(x => x.UseNpgsql(defaultDb));

// Marten - Event Sourcing for PaymentProcessing
var martenConnectionString = builder.Configuration.GetConnectionString("defaultDb");
builder.Services.AddMarten(opts =>
    {
        opts.Connection(martenConnectionString!);
        opts.Projections.Snapshot<PaymentTransaction>(SnapshotLifecycle.Inline);
        opts.Schema.For<PaymentTransaction>()
            .UniqueIndex(UniqueIndexType.Computed, t => t.MerchantId, t => t.OrderId);
        opts.Schema.For<BinRecord>()
            .Index(x => x.BinEightStart)
            .Index(x => x.BinEightEnd);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

// Caching
builder.Services.AddCachingServices();
builder.Services.AddRedisCache(builder.Configuration.GetConnectionString("redis"));

// Auth
builder.Services.LoadCurrentUser();

// Dependencies
builder.Services.AddAllDependencies();

// Wolverine
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq");

builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.UseDurableLocalQueues();

    opts.Policies.AddMiddleware(typeof(CacheInvalidationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<CacheResultAttribute>() != null
                 && chain.MessageType.Name.EndsWith("Command"));

    //RequiresMerchantAttribute attribute'u kullanan class içerisinde kullanılabilir 
    opts.Policies.AddMiddleware(typeof(MerchantMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiresMerchantAttribute>() != null);

    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

    if (!string.IsNullOrEmpty(rabbitMqConnectionString))
    {
        opts.UseRabbitMq(new Uri(rabbitMqConnectionString))
            .AutoProvision();
    }
});

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payment Gateway API",
        Version = "v1"
    });
});

// Http
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("webhook", client => { client.Timeout = TimeSpan.FromSeconds(10); });

// gRPC Bank Clients
builder.Services
    .AddGrpcClient<PaymentGateway.BankContracts.BankPaymentService.BankPaymentServiceClient>("garanti",
        o => { o.Address = new Uri("https+http://garanti-service"); }).AddServiceDiscovery();

// Cors
builder.Services.AddCors();

// Controllers
builder.Services.AddControllers();

// Endpoints
builder.Services.AddEndpointsApiExplorer();

// Health Check
builder.Services.AddHealthChecks();

var app = builder.Build();

// Health Check
app.MapHealthChecks("/health");

// CORS
app.UseCors(policyBuilder => policyBuilder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

UpdateDatabase(app);
DataSeeder.Seed(app);

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Gateway v1"));

app.UseRouting();
app.UseHttpsRedirection();
app.UseExceptionHandler();
app.MapControllers();

var api = app.MapGroup("/api");
api.MapAuthEndpoints();
api.MapUserEndpoints();
api.MapRoleEndpoints();
api.MapBankEndpoints();
api.MapMerchantBankEndpoints();
api.MapBinRecordEndpoints();
api.MapBankCommissionEndpoints();
api.MapMerchantCommissionEndpoints();
api.MapMerchantEndpoints();
api.MapPaymentTransactionEndpoints();
api.MapMerchantBalanceEndpoints();
api.MapSettlementEndpoints();

app.Run();


static void UpdateDatabase(IApplicationBuilder app)
{
    using var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
    var sp = scope.ServiceProvider;

    var contexts = new DbContext[]
    {
        sp.GetRequiredService<BankIntegrationContext>(),
        sp.GetRequiredService<CommissionManagementContext>(),
        sp.GetRequiredService<IamContext>(),
        sp.GetRequiredService<MerchantManagementContext>(),
        sp.GetRequiredService<SettlementContext>(),
    };

    foreach (var context in contexts)
        context.Database.Migrate();
}