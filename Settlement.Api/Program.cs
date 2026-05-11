using System.Text.Json;
using PaymentGatewayApi.Dependencies;
using PaymentGatewayApi.Exceptions;
using PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Endpoints;
using Settlement.Api.Auths;
using Settlement.Api.Settlement.Settlements.Features.Endpoints;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

builder.Services.AddHttpContextAccessor();
builder.Services.LoadCurrentUser();

var settlementDb = builder.Configuration.GetConnectionString("settlementDb")!;
builder.Services.AddMarten(opts =>
{
    opts.Connection(settlementDb);
    opts.UseNewtonsoftForSerialization(
        nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
        configure: s =>
        {
            s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
        });
    opts.Schema.For<Settlement.Api.Settlement.Settlements.Settlement>();
    opts.Schema.For<PaymentGatewayApi.Modules.Settlement.MerchantBalances.MerchantBalance>();
})
.IntegrateWithWolverine()
.ApplyAllDatabaseChangesOnStartup();

var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.MapSettlementEndpoints();
app.MapMerchantBalanceEndpoints();
app.Run();