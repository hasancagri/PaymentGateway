using MerchantManagement.Api;
using MerchantManagement.Api.Auths;
using MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Endpoints;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.RabbitMQ;
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// Auth
builder.Services.AddHttpContextAccessor();
builder.Services.LoadCurrentUser();

// Caching (required for JwtPermissionFilter)
builder.Services.AddCachingServices();
var redisConn = builder.Configuration.GetConnectionString("redis");
if (!string.IsNullOrEmpty(redisConn))
    builder.Services.AddRedisCache(redisConn);

var connString = builder.Configuration.GetConnectionString("merchantDb")!;
var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;

builder.Services.AddDbContextWithWolverineIntegration<MerchantManagementContext>(
    opts => opts.UseNpgsql(connString));

builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    opts.PublishMessage<SharedMerchantEvents.MerchantCreated>().ToRabbitExchange("merchant.created");
    opts.PublishMessage<SharedMerchantEvents.MerchantUpdated>().ToRabbitExchange("merchant.updated");
    opts.PublishMessage<SharedMerchantEvents.MerchantStatusChanged>().ToRabbitExchange("merchant.status-changed");
    opts.PublishMessage<SharedMerchantEvents.ApiKeyGenerated>().ToRabbitExchange("merchant.api-key-generated");
    opts.PublishMessage<SharedMerchantEvents.ApiKeyRevoked>().ToRabbitExchange("merchant.api-key-revoked");
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.MapMerchantEndpoints();
app.Run();