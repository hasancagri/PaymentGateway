using MerchantManagement.Api.Domains.Merchants;
using MerchantManagement.Api.Domains.Merchants.Features.Endpoints;
using MerchantManagement.Api.Exceptions;
using Wolverine.Marten;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

builder.Services.AddHttpContextAccessor();
builder.Services.LoadCurrentUser();

builder.Services.AddCachingServices();
var redisConn = builder.Configuration.GetConnectionString("redis");
if (!string.IsNullOrEmpty(redisConn))
    builder.Services.AddRedisCache(redisConn);

var merchantDb = builder.Configuration.GetConnectionString("merchantDb")!;
builder.Services.AddMarten(opts =>
{
    opts.Connection(merchantDb);
    opts.UseNewtonsoftForSerialization(
        nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
        configure: s =>
        {
            s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
        });
    opts.Schema.For<Merchant>();
})
.IntegrateWithWolverine()
.ApplyAllDatabaseChangesOnStartup();

var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    opts.PublishMessage<MerchantCreated>().ToRabbitExchange("merchant.created");
    opts.PublishMessage<MerchantUpdated>().ToRabbitExchange("merchant.updated");
    opts.PublishMessage<MerchantStatusChanged>().ToRabbitExchange("merchant.status-changed");
    opts.PublishMessage<ApiKeyGenerated>().ToRabbitExchange("merchant.api-key-generated");
    opts.PublishMessage<ApiKeyRevoked>().ToRabbitExchange("merchant.api-key-revoked");
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.MapMerchantEndpoints();
app.Run();