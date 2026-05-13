using IAM.Api.Dependencies;
using IAM.Api.Domains.Users.Features.Endpoints;
using IAM.Api.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKeycloakJwtAuthentication();

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

builder.Services.AddGlobalExceptionHandler();

var iamDb = builder.Configuration.GetConnectionString("iamDb");
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = SchemaConstants.IAM_SCHEMA_NAME;
    opts.Connection(iamDb!);
    opts.UseNewtonsoftForSerialization(
        nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
        configure: s =>
        {
            s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
        });
    opts.Schema.For<User>().Index(u => u.Email.Value);
    opts.Schema.For<Role>();
})
.IntegrateWithWolverine()
.ApplyAllDatabaseChangesOnStartup();

builder.Services.AddCachingServices();
builder.Services.AddRedisCache(builder.Configuration.GetConnectionString("redis"));

builder.Services.AddAllDependencies();
builder.Services.LoadCurrentUser();

builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();

    opts.Policies.AddMiddleware(typeof(CacheInvalidationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<CacheResultAttribute>() != null
                 && chain.MessageType.Name.EndsWith("Command"));

    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payment Gateway IAM API",
        Version = "v1"
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddCors();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.UseCors(policyBuilder => policyBuilder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Gateway IAM v1"));

app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler();
app.MapControllers();

var api = app.MapGroup("/api");
api.MapUserEndpoints();
api.MapRoleEndpoints();

app.Run();