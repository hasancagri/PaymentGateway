using MerchantManagement.Api.Domains.Merchants;
using MerchantManagement.Api.Domains.Merchants.Features.Endpoints;
using MerchantManagement.Api.Exceptions;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddKeycloakJwtAuthentication();

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
    opts.DatabaseSchemaName = SchemaConstants.MERCHANT_MANAGEMENT_SCHEMA_NAME;
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

builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler();
app.MapMerchantEndpoints();
app.Run();