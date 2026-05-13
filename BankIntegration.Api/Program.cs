using System.Reflection;
using System.Text.Json;
using BankIntegration.Api.Auths;
using BankIntegration.Api.Dependencies;
using BankIntegration.Api.Domains.Banks.Features.Endpoints;
using BankIntegration.Api.Domains.MerchantBanks.Features.Endpoints;
using BankIntegration.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddKeycloakJwtAuthentication();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

builder.Services.AddHttpContextAccessor();
builder.Services.LoadCurrentUser();

var bankIntDb = builder.Configuration.GetConnectionString("bankIntegrationDb")!;
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.BANK_INTEGRATION_SCHEMA_NAME;
    opts.Connection(bankIntDb);
    opts.UseNewtonsoftForSerialization(
        nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
        configure: s =>
        {
            s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
        });
    opts.Schema.For<BankIntegration.Api.Domains.Banks.Bank>();
    opts.Schema.For<MerchantBank>();
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

var api = app.MapGroup("/api");
api.MapBankEndpoints();
api.MapMerchantBankEndpoints();

app.Run();