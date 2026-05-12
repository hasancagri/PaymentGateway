using IAM.Api.Dependencies;
using IAM.Api.Domains.Users.Features.Endpoints;
using IAM.Api.Exceptions;

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

// Marten document store — User ve Role JSON document olarak saklanır
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

// Caching
builder.Services.AddCachingServices();
builder.Services.AddRedisCache(builder.Configuration.GetConnectionString("redis"));

// Auth
builder.Services.LoadCurrentUser();

// Dependencies
builder.Services.AddAllDependencies();

// Wolverine
builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();

    opts.Policies.AddMiddleware(typeof(CacheInvalidationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<CacheResultAttribute>() != null
                 && chain.MessageType.Name.EndsWith("Command"));

    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
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

app.Run();

