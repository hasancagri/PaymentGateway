using CommissionManagement.Api.Domains.BankCommissions;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.LoadCurrentUser();

var commissionDb = builder.Configuration.GetConnectionString("commissionDb")!;
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.COMMISSION_MANAGEMENT_SCHEMA_NAME;
    opts.Connection(commissionDb);
    opts.UseNewtonsoftForSerialization(
        nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
        configure: s =>
        {
            s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
        });
    opts.Schema.For<BankCommission>();
    opts.Schema.For<MerchantCommission>();
})
.IntegrateWithWolverine()
.ApplyAllDatabaseChangesOnStartup();

var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    opts.PublishMessage<BankCommissionUpdated>().ToRabbitExchange(ExchangeConstants.BankCommissionUpdated);
    opts.PublishMessage<MerchantCommissionUpdated>().ToRabbitExchange(ExchangeConstants.MerchantCommissionUpdated);
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseExceptionHandler();

var api = app.MapGroup("/api");
api.MapBankCommissionEndpoints();
api.MapMerchantCommissionEndpoints();

app.Run();