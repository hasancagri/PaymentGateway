

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddKeycloakJwtAuthentication();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

builder.Services.AddHttpContextAccessor();
builder.Services.LoadCurrentUser();

var settlementDb = builder.Configuration.GetConnectionString("settlementDb")!;
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.SETTLEMENT_SCHEMA_NAME;
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
app.MapSettlementEndpoints();
app.MapMerchantBalanceEndpoints();
app.Run();