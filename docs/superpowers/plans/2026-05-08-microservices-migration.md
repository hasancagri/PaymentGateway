# Microservices Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract PaymentGatewayApi's 6 bounded context modules into independent microservices, each with its own Postgres database, communicating via RabbitMQ events and a JWT-issuing ApiGateway.

**Architecture:** Each bounded context becomes a standalone ASP.NET Core service with its own Postgres database. A new ApiGateway validates API keys via a Redis read model (fed by RabbitMQ events from MerchantManagement) and issues HMAC-SHA256 JWTs. Downstream services validate JWTs independently. Services maintain local Marten read models for cross-domain data, synchronized via RabbitMQ events. BankSelector's cross-context dependency is resolved by a pre-computed `BankRouteSummary` read model published by BankIntegration.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, Wolverine, Marten (PaymentProcessing + read models), EF Core + Npgsql (all others), YARP, RabbitMQ, Redis, PostgreSQL, Aspire

---

## Target Solution Structure

```
PaymentGateway/
├── AppHost/                          (existing — updated)
├── PaymentGateway.SharedContracts/   (NEW — integration events)
├── ApiGateway/                       (NEW — API key → JWT, YARP proxy)
├── MerchantManagement.Api/           (NEW — extracted from PaymentGatewayApi)
├── PaymentProcessing.Api/            (NEW — extracted from PaymentGatewayApi)
├── BankIntegration.Api/              (NEW — extracted from PaymentGatewayApi)
├── CommissionManagement.Api/         (NEW — extracted from PaymentGatewayApi)
├── IAM.Api/                          (NEW — extracted from PaymentGatewayApi)
├── Settlement.Api/                   (NEW — extracted from PaymentGatewayApi)
├── PaymentGatewayBff/                (existing — updated to use Gateway)
├── PaymentGatewayPortal/             (existing — unchanged)
├── Banks/                            (existing — unchanged)
├── Common/                           (existing — unchanged)
├── ServiceDefaults/                  (existing — unchanged)
└── PaymentGateway.BankContracts/     (existing — unchanged)
```

---

## Phase 1: Foundation

### Task 1: Create PaymentGateway.SharedContracts

**Files:**
- Create: `PaymentGateway.SharedContracts/PaymentGateway.SharedContracts.csproj`
- Create: `PaymentGateway.SharedContracts/MerchantEvents.cs`
- Create: `PaymentGateway.SharedContracts/PaymentEvents.cs`
- Create: `PaymentGateway.SharedContracts/BankIntegrationEvents.cs`

Integration events are the contracts between services over RabbitMQ. All services reference this library.

- [ ] **Step 1: Create and add to solution**
```bash
dotnet new classlib -n PaymentGateway.SharedContracts -o PaymentGateway.SharedContracts
dotnet sln PaymentGateway.slnx add PaymentGateway.SharedContracts/PaymentGateway.SharedContracts.csproj
rm PaymentGateway.SharedContracts/Class1.cs
```

- [ ] **Step 2: Write merchant integration events**

Create `PaymentGateway.SharedContracts/MerchantEvents.cs`:
```csharp
namespace PaymentGateway.SharedContracts.MerchantEvents;

public sealed record MerchantCreated(Guid MerchantId, string Name, string Email, string Country, DateTime OccurredOn);
public sealed record MerchantUpdated(Guid MerchantId, string? Name, string? WebhookUrl, DateTime OccurredOn);
public sealed record MerchantStatusChanged(Guid MerchantId, string OldStatus, string NewStatus, DateTime OccurredOn);
public sealed record ApiKeyGenerated(Guid MerchantId, string KeyHash, DateTime OccurredOn);
public sealed record ApiKeyRevoked(Guid MerchantId, string KeyHash, DateTime OccurredOn);
```

- [ ] **Step 3: Write payment integration events**

Create `PaymentGateway.SharedContracts/PaymentEvents.cs`:
```csharp
namespace PaymentGateway.SharedContracts.PaymentEvents;

public sealed record PaymentApprovedIntegration(Guid TransactionId, Guid MerchantId, string OrderId, decimal Amount, string Currency, decimal MerchantAmount, DateTime OccurredOn);
public sealed record PaymentDeclinedIntegration(Guid TransactionId, Guid MerchantId, string OrderId, string ResultCode, DateTime OccurredOn);
public sealed record PaymentFailedIntegration(Guid TransactionId, Guid MerchantId, string OrderId, string Reason, DateTime OccurredOn);
```

- [ ] **Step 4: Write bank integration events**

Create `PaymentGateway.SharedContracts/BankIntegrationEvents.cs`:
```csharp
namespace PaymentGateway.SharedContracts.BankIntegrationEvents;

// Published by BankIntegration.Api whenever a merchant's routing configuration changes.
// PaymentProcessing consumes this to maintain its local BankRouteSummary read model.
public sealed record BankRouteSynced(
    Guid MerchantId,
    Guid BankId,
    string BankName,
    string Currency,
    decimal BankRate,
    decimal MerchantRate,
    DateTime OccurredOn);
```

- [ ] **Step 5: Commit**
```bash
git add PaymentGateway.SharedContracts/
git commit -m "feat: add SharedContracts library with integration events"
```

---

### Task 2: Scaffold new service projects

**Files:**
- Create: `ApiGateway/ApiGateway.csproj`
- Create: `MerchantManagement.Api/MerchantManagement.Api.csproj`
- Create: `PaymentProcessing.Api/PaymentProcessing.Api.csproj`
- Create: `BankIntegration.Api/BankIntegration.Api.csproj`
- Create: `CommissionManagement.Api/CommissionManagement.Api.csproj`
- Create: `IAM.Api/IAM.Api.csproj`
- Create: `Settlement.Api/Settlement.Api.csproj`

- [ ] **Step 1: Create all service projects**
```bash
dotnet new web -n ApiGateway -o ApiGateway
dotnet new web -n MerchantManagement.Api -o MerchantManagement.Api
dotnet new web -n PaymentProcessing.Api -o PaymentProcessing.Api
dotnet new web -n BankIntegration.Api -o BankIntegration.Api
dotnet new web -n CommissionManagement.Api -o CommissionManagement.Api
dotnet new web -n IAM.Api -o IAM.Api
dotnet new web -n Settlement.Api -o Settlement.Api
```

- [ ] **Step 2: Add to solution**
```bash
dotnet sln PaymentGateway.slnx add ApiGateway/ApiGateway.csproj
dotnet sln PaymentGateway.slnx add MerchantManagement.Api/MerchantManagement.Api.csproj
dotnet sln PaymentGateway.slnx add PaymentProcessing.Api/PaymentProcessing.Api.csproj
dotnet sln PaymentGateway.slnx add BankIntegration.Api/BankIntegration.Api.csproj
dotnet sln PaymentGateway.slnx add CommissionManagement.Api/CommissionManagement.Api.csproj
dotnet sln PaymentGateway.slnx add IAM.Api/IAM.Api.csproj
dotnet sln PaymentGateway.slnx add Settlement.Api/Settlement.Api.csproj
```

- [ ] **Step 3: Add ServiceDefaults + SharedContracts references to all new services**
```bash
for svc in ApiGateway MerchantManagement.Api PaymentProcessing.Api BankIntegration.Api CommissionManagement.Api IAM.Api Settlement.Api; do
  dotnet add $svc/$svc.csproj reference ServiceDefaults/ServiceDefaults.csproj
  dotnet add $svc/$svc.csproj reference PaymentGateway.SharedContracts/PaymentGateway.SharedContracts.csproj
done
```

- [ ] **Step 4: Commit**
```bash
git add .
git commit -m "feat: scaffold all microservice projects"
```

---

### Task 3: Update AppHost

**Files:**
- Modify: `AppHost/AppHost.cs`
- Modify: `AppHost/AppHost.csproj`

Each service gets its own named Postgres database. The existing `defaultDb` is kept for GarantiService (Banks).

- [ ] **Step 1: Add project references to AppHost**
```bash
dotnet add AppHost/AppHost.csproj reference ApiGateway/ApiGateway.csproj
dotnet add AppHost/AppHost.csproj reference MerchantManagement.Api/MerchantManagement.Api.csproj
dotnet add AppHost/AppHost.csproj reference PaymentProcessing.Api/PaymentProcessing.Api.csproj
dotnet add AppHost/AppHost.csproj reference BankIntegration.Api/BankIntegration.Api.csproj
dotnet add AppHost/AppHost.csproj reference CommissionManagement.Api/CommissionManagement.Api.csproj
dotnet add AppHost/AppHost.csproj reference IAM.Api/IAM.Api.csproj
dotnet add AppHost/AppHost.csproj reference Settlement.Api/Settlement.Api.csproj
```

- [ ] **Step 2: Rewrite AppHost.cs**

Replace `AppHost/AppHost.cs` entirely:
```csharp
var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin().WithLifetime(ContainerLifetime.Persistent);
var redis = builder.AddRedis("redis").WithLifetime(ContainerLifetime.Persistent);
var postgres = builder.AddPostgres("postgres").WithPgAdmin().WithDataVolume().WithLifetime(ContainerLifetime.Persistent);

// Dedicated database per service (Database per Service pattern)
var garantiDb       = postgres.AddDatabase("defaultDb");         // kept for GarantiService
var merchantDb      = postgres.AddDatabase("merchantDb");
var paymentDb       = postgres.AddDatabase("paymentDb");
var bankIntDb       = postgres.AddDatabase("bankIntegrationDb");
var commissionDb    = postgres.AddDatabase("commissionDb");
var iamDb           = postgres.AddDatabase("iamDb");
var settlementDb    = postgres.AddDatabase("settlementDb");
var gatewayDb       = postgres.AddDatabase("gatewayDb");         // Wolverine outbox for ApiGateway

// JWT secret shared across ApiGateway and all downstream services
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);

var garanti = builder.AddProject<Projects.GarantiService>("garanti")
    .WithReference(garantiDb).WaitFor(garantiDb);

var merchantApi = builder.AddProject<Projects.MerchantManagement_Api>("merchant-management")
    .WithReference(rabbitmq).WithReference(merchantDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(merchantDb);

var gateway = builder.AddProject<Projects.ApiGateway>("api-gateway")
    .WithReference(rabbitmq).WithReference(redis).WithReference(gatewayDb)
    .WithReference(merchantApi)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(redis).WaitFor(gatewayDb).WaitFor(merchantApi);

var bankIntApi = builder.AddProject<Projects.BankIntegration_Api>("bank-integration")
    .WithReference(rabbitmq).WithReference(bankIntDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(bankIntDb);

var commissionApi = builder.AddProject<Projects.CommissionManagement_Api>("commission-management")
    .WithReference(rabbitmq).WithReference(commissionDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(commissionDb);

var paymentApi = builder.AddProject<Projects.PaymentProcessing_Api>("payment-processing")
    .WithReference(rabbitmq).WithReference(paymentDb).WithReference(garanti)
    .WithReference(bankIntApi).WithReference(commissionApi)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(paymentDb).WaitFor(garanti).WaitFor(bankIntApi).WaitFor(commissionApi);

var iamApi = builder.AddProject<Projects.IAM_Api>("iam")
    .WithReference(rabbitmq).WithReference(iamDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(iamDb);

var settlementApi = builder.AddProject<Projects.Settlement_Api>("settlement")
    .WithReference(rabbitmq).WithReference(settlementDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(settlementDb);

var bff = builder.AddProject<Projects.PaymentGatewayBff>("payment-gateway-bff")
    .WithReference(gateway).WaitFor(gateway);

builder.AddProject<Projects.PaymentGatewayPortal>("payment-gateway-portal")
    .WithReference(bff).WaitFor(bff);

builder.Build().Run();
```

- [ ] **Step 3: Add jwt-secret parameter to AppHost user secrets**
```bash
dotnet user-secrets --project AppHost set "Parameters:jwt-secret" "change-this-to-a-32-char-minimum-secret-key"
```

- [ ] **Step 4: Commit**
```bash
git add AppHost/
git commit -m "feat: update AppHost — database per service, JWT secret parameter"
```

---

## Phase 2: MerchantManagement.Api

### Task 4: Setup MerchantManagement.Api

**Files:**
- Create: `MerchantManagement.Api/Program.cs`
- Copy: `PaymentGatewayApi/Modules/MerchantManagement/` → `MerchantManagement.Api/Modules/MerchantManagement/`
- Copy: `PaymentGatewayApi/Contexts/MerchantManagementContext.cs` → `MerchantManagement.Api/MerchantManagementContext.cs`

- [ ] **Step 1: Add NuGet packages**
```bash
dotnet add MerchantManagement.Api/MerchantManagement.Api.csproj package WolverineFx
dotnet add MerchantManagement.Api/MerchantManagement.Api.csproj package WolverineFx.Http
dotnet add MerchantManagement.Api/MerchantManagement.Api.csproj package WolverineFx.RabbitMQ
dotnet add MerchantManagement.Api/MerchantManagement.Api.csproj package WolverineFx.Postgresql
dotnet add MerchantManagement.Api/MerchantManagement.Api.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add MerchantManagement.Api/MerchantManagement.Api.csproj reference Common/Common.csproj
```

- [ ] **Step 2: Copy module files and fix namespaces**
```bash
cp -r PaymentGatewayApi/Modules/MerchantManagement MerchantManagement.Api/Modules/MerchantManagement
cp PaymentGatewayApi/Contexts/MerchantManagementContext.cs MerchantManagement.Api/MerchantManagementContext.cs
```

Then in all copied `.cs` files, replace namespace prefix:
- `PaymentGatewayApi.Modules.MerchantManagement` → `MerchantManagement.Api.Modules.MerchantManagement`
- `PaymentGatewayApi.Contexts` → `MerchantManagement.Api`

```bash
find MerchantManagement.Api/ -name "*.cs" -exec sed -i '' \
  's/PaymentGatewayApi\.Modules\.MerchantManagement/MerchantManagement.Api.Modules.MerchantManagement/g;
   s/PaymentGatewayApi\.Contexts/MerchantManagement.Api/g' {} \;
```

- [ ] **Step 3: Write Program.cs**

Create `MerchantManagement.Api/Program.cs`:
```csharp
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
app.MapMerchantEndpoints();
app.Run();
```

- [ ] **Step 4: Verify build**
```bash
dotnet build MerchantManagement.Api/MerchantManagement.Api.csproj
```
Expected: no errors. Fix any missing using statements or namespace mismatches.

- [ ] **Step 5: Commit**
```bash
git add MerchantManagement.Api/
git commit -m "feat: setup MerchantManagement.Api — EF Core, Wolverine, RabbitMQ routing"
```

---

### Task 5: Publish integration events from MerchantManagement handlers

**Files:**
- Modify: `MerchantManagement.Api/Modules/MerchantManagement/Merchants/Features/Commands/CreateMerchant.cs`
- Modify: `MerchantManagement.Api/Modules/MerchantManagement/Merchants/Features/Commands/GenerateApiKey.cs`
- Modify: `MerchantManagement.Api/Modules/MerchantManagement/Merchants/Features/Commands/RevokeApiKey.cs`
- Modify: `MerchantManagement.Api/Modules/MerchantManagement/Merchants/Features/Commands/UpdateMerchant.cs`
- Modify: `MerchantManagement.Api/Modules/MerchantManagement/Merchants/Features/Commands/ActivateMerchant.cs`
- Modify: `MerchantManagement.Api/Modules/MerchantManagement/Merchants/Features/Commands/DeactivateMerchant.cs`
- Modify: `MerchantManagement.Api/Modules/MerchantManagement/Merchants/Features/Commands/SuspendMerchant.cs`

Wolverine automatically publishes any message a handler returns. Returning the integration event alongside the existing result routes it to RabbitMQ.

- [ ] **Step 1: Update CreateMerchant handler**

In `CreateMerchant.cs`, add to using section:
```csharp
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;
```

Change the `Handle` method signature and return to include the integration event:
```csharp
public static async Task<(FeatureObjectResultModel<CreateMerchantResponse>, SharedMerchantEvents.MerchantCreated?)> Handle(
    CreateMerchantCommand cmd,
    MerchantManagementContext db,
    CancellationToken ct)
{
    // ... keep all existing logic unchanged ...

    // After db.SaveChangesAsync / merchant creation, before returning:
    var integrationEvent = new SharedMerchantEvents.MerchantCreated(
        merchant.Id,
        merchant.Name.Value,
        merchant.ContactInfo.Email,
        merchant.Address.Country,
        DateTime.UtcNow);

    return (FeatureObjectResultModel<CreateMerchantResponse>.Ok(response), integrationEvent);
}
```

- [ ] **Step 2: Update GenerateApiKey handler**

The `KeyHash` is the SHA256 hex string already stored in the DB as `ApiKeyValue`. Return it in the integration event so the Gateway can store the hash in Redis:

```csharp
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

// Change return type:
public static async Task<(FeatureObjectResultModel<GenerateApiKeyResponse>, SharedMerchantEvents.ApiKeyGenerated?)> Handle(...)
{
    // ... existing logic ...
    // keyHash is the hex string stored in ApiKeyValue — use that same value:
    var integrationEvent = new SharedMerchantEvents.ApiKeyGenerated(
        merchant.Id,
        keyHash,      // SHA256 hex string, same value as ApiKeyValue.Hash
        DateTime.UtcNow);

    return (FeatureObjectResultModel<GenerateApiKeyResponse>.Ok(response), integrationEvent);
}
```

- [ ] **Step 3: Update RevokeApiKey handler**

```csharp
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

public static async Task<(FeatureObjectResultModel<RevokeApiKeyResponse>, SharedMerchantEvents.ApiKeyRevoked?)> Handle(...)
{
    // ... existing logic to find and revoke the key ...
    var integrationEvent = new SharedMerchantEvents.ApiKeyRevoked(
        merchant.Id,
        revokedKey.KeyValue.Hash,  // the hash of the revoked key
        DateTime.UtcNow);

    return (FeatureObjectResultModel<RevokeApiKeyResponse>.Ok(response), integrationEvent);
}
```

- [ ] **Step 4: Update status change handlers (Activate, Deactivate, Suspend)**

Each follows the same pattern — add the integration event to the return:
```csharp
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

// Change return type to include MerchantStatusChanged:
public static async Task<(FeatureObjectResultModel<...>, SharedMerchantEvents.MerchantStatusChanged?)> Handle(...)
{
    // ... existing logic ...
    var integrationEvent = new SharedMerchantEvents.MerchantStatusChanged(
        merchant.Id,
        oldStatus.ToString(),
        newStatus.ToString(),
        DateTime.UtcNow);

    return (result, integrationEvent);
}
```

- [ ] **Step 5: Update UpdateMerchant handler**

```csharp
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

public static async Task<(FeatureObjectResultModel<UpdateMerchantResponse>, SharedMerchantEvents.MerchantUpdated?)> Handle(...)
{
    // ... existing logic ...
    var integrationEvent = new SharedMerchantEvents.MerchantUpdated(
        merchant.Id,
        cmd.Name,
        cmd.WebhookUrl,
        DateTime.UtcNow);

    return (result, integrationEvent);
}
```

- [ ] **Step 6: Verify build**
```bash
dotnet build MerchantManagement.Api/MerchantManagement.Api.csproj
```

- [ ] **Step 7: Commit**
```bash
git add MerchantManagement.Api/
git commit -m "feat: publish integration events from MerchantManagement handlers"
```

---

## Phase 3: ApiGateway

### Task 6: ApiGateway — JWT service and Redis read model handlers

**Files:**
- Create: `ApiGateway/JwtOptions.cs`
- Create: `ApiGateway/JwtService.cs`
- Create: `ApiGateway/Handlers/MerchantEventHandlers.cs`
- Create: `ApiGateway/Program.cs`

- [ ] **Step 1: Add NuGet packages**
```bash
dotnet add ApiGateway/ApiGateway.csproj package WolverineFx
dotnet add ApiGateway/ApiGateway.csproj package WolverineFx.RabbitMQ
dotnet add ApiGateway/ApiGateway.csproj package WolverineFx.Postgresql
dotnet add ApiGateway/ApiGateway.csproj package StackExchange.Redis
dotnet add ApiGateway/ApiGateway.csproj package System.IdentityModel.Tokens.Jwt
dotnet add ApiGateway/ApiGateway.csproj package Yarp.ReverseProxy
dotnet add ApiGateway/ApiGateway.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
```

- [ ] **Step 2: Create JwtOptions**

Create `ApiGateway/JwtOptions.cs`:
```csharp
namespace ApiGateway;

public class JwtOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "payment-gateway";
    public string Audience { get; set; } = "payment-gateway-services";
    public int ExpiryMinutes { get; set; } = 15;
}
```

- [ ] **Step 3: Create JwtService**

Create `ApiGateway/JwtService.cs`:
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway;

public class JwtService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opts = options.Value;

    public string GenerateToken(Guid merchantId, string merchantName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("merchant_id", merchantId.ToString()),
            new Claim("merchant_name", merchantName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opts.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 4: Create Redis event handlers**

Create `ApiGateway/Handlers/MerchantEventHandlers.cs`:
```csharp
using PaymentGateway.SharedContracts.MerchantEvents;
using StackExchange.Redis;

namespace ApiGateway.Handlers;

public static class MerchantEventHandlers
{
    // Stores merchant name for JWT payload
    public static async Task Handle(MerchantCreated evt, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        await db.HashSetAsync($"merchant:{evt.MerchantId}", [
            new HashEntry("name", evt.Name),
            new HashEntry("status", "Active")
        ]);
    }

    // Stores api_key_hash → merchant_id + name mapping used at validation time
    public static async Task Handle(ApiKeyGenerated evt, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        var merchantName = await db.HashGetAsync($"merchant:{evt.MerchantId}", "name");
        await db.HashSetAsync($"apikey:{evt.KeyHash}", [
            new HashEntry("merchant_id", evt.MerchantId.ToString()),
            new HashEntry("merchant_name", merchantName.HasValue ? merchantName.ToString() : string.Empty),
            new HashEntry("status", "Active")
        ]);
    }

    public static async Task Handle(ApiKeyRevoked evt, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"apikey:{evt.KeyHash}");
    }

    public static async Task Handle(MerchantStatusChanged evt, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        await db.HashSetAsync($"merchant:{evt.MerchantId}", "status", evt.NewStatus);
        // Note: individual key statuses are invalidated on next request if merchant is suspended.
        // For immediate revocation, maintain a merchant→keys set and iterate it here.
    }
}
```

- [ ] **Step 5: Write Program.cs**

Create `ApiGateway/Program.cs`:
```csharp
using ApiGateway;
using ApiGateway.Handlers;
using StackExchange.Redis;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
var redisConn = builder.Configuration.GetConnectionString("redis")!;

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddSingleton<JwtService>();

builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    opts.ListenToRabbitQueue("gateway.merchant-events")
        .BindExchange("merchant.created")
        .BindExchange("merchant.api-key-generated")
        .BindExchange("merchant.api-key-revoked")
        .BindExchange("merchant.status-changed");
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapReverseProxy(pipeline =>
{
    pipeline.Use(async (ctx, next) =>
    {
        var apiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
        {
            var jwtSvc = ctx.RequestServices.GetRequiredService<JwtService>();
            var redis = ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
            var db = redis.GetDatabase();

            var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
            var merchantId = await db.HashGetAsync($"apikey:{keyHash}", "merchant_id");
            var merchantName = await db.HashGetAsync($"apikey:{keyHash}", "merchant_name");
            var status = await db.HashGetAsync($"apikey:{keyHash}", "status");

            if (!merchantId.IsNull && status == "Active")
            {
                var token = jwtSvc.GenerateToken(Guid.Parse(merchantId!), merchantName!);
                ctx.Request.Headers["Authorization"] = $"Bearer {token}";
                ctx.Request.Headers.Remove("X-Api-Key");
            }
            else
            {
                ctx.Response.StatusCode = 401;
                return;
            }
        }
        await next(ctx);
    });
});

app.Run();
```

- [ ] **Step 6: Add appsettings.json**

Create `ApiGateway/appsettings.json`:
```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "Jwt": {
    "Issuer": "payment-gateway",
    "Audience": "payment-gateway-services",
    "ExpiryMinutes": 15
  },
  "ReverseProxy": {
    "Routes": {
      "merchant-route": {
        "ClusterId": "merchant-cluster",
        "Match": { "Path": "/merchants/{**catch-all}" }
      },
      "payment-route": {
        "ClusterId": "payment-cluster",
        "Match": { "Path": "/payments/{**catch-all}" }
      },
      "bank-integration-route": {
        "ClusterId": "bank-integration-cluster",
        "Match": { "Path": "/banks/{**catch-all}" }
      },
      "commission-route": {
        "ClusterId": "commission-cluster",
        "Match": { "Path": "/commissions/{**catch-all}" }
      },
      "iam-route": {
        "ClusterId": "iam-cluster",
        "Match": { "Path": "/iam/{**catch-all}" }
      },
      "settlement-route": {
        "ClusterId": "settlement-cluster",
        "Match": { "Path": "/settlements/{**catch-all}" }
      }
    },
    "Clusters": {
      "merchant-cluster":      { "Destinations": { "d1": { "Address": "http://merchant-management" } } },
      "payment-cluster":       { "Destinations": { "d1": { "Address": "http://payment-processing" } } },
      "bank-integration-cluster": { "Destinations": { "d1": { "Address": "http://bank-integration" } } },
      "commission-cluster":    { "Destinations": { "d1": { "Address": "http://commission-management" } } },
      "iam-cluster":           { "Destinations": { "d1": { "Address": "http://iam" } } },
      "settlement-cluster":    { "Destinations": { "d1": { "Address": "http://settlement" } } }
    }
  }
}
```

- [ ] **Step 7: Verify build**
```bash
dotnet build ApiGateway/ApiGateway.csproj
```

- [ ] **Step 8: Commit**
```bash
git add ApiGateway/
git commit -m "feat: ApiGateway — Redis read model, JWT issuance, YARP proxy with X-Api-Key middleware"
```

---

## Phase 4: PaymentProcessing.Api

### Task 7: Setup PaymentProcessing.Api

**Files:**
- Create: `PaymentProcessing.Api/Program.cs`
- Copy: `PaymentGatewayApi/Modules/PaymentProcessing/` → `PaymentProcessing.Api/Modules/PaymentProcessing/`

- [ ] **Step 1: Add NuGet packages**
```bash
dotnet add PaymentProcessing.Api/PaymentProcessing.Api.csproj package WolverineFx
dotnet add PaymentProcessing.Api/PaymentProcessing.Api.csproj package WolverineFx.Http
dotnet add PaymentProcessing.Api/PaymentProcessing.Api.csproj package WolverineFx.Marten
dotnet add PaymentProcessing.Api/PaymentProcessing.Api.csproj package WolverineFx.RabbitMQ
dotnet add PaymentProcessing.Api/PaymentProcessing.Api.csproj package Marten
dotnet add PaymentProcessing.Api/PaymentProcessing.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add PaymentProcessing.Api/PaymentProcessing.Api.csproj reference Common/Common.csproj
dotnet add PaymentProcessing.Api/PaymentProcessing.Api.csproj reference PaymentGateway.BankContracts/PaymentGateway.BankContracts.csproj
```

- [ ] **Step 2: Copy module files and fix namespaces**
```bash
cp -r PaymentGatewayApi/Modules/PaymentProcessing PaymentProcessing.Api/Modules/PaymentProcessing

find PaymentProcessing.Api/ -name "*.cs" -exec sed -i '' \
  's/PaymentGatewayApi\.Modules\.PaymentProcessing/PaymentProcessing.Api.Modules.PaymentProcessing/g' {} \;
```

Remove references to `MerchantManagementContext`, `BankIntegrationContext`, `CommissionManagementContext` from any using statements in copied files — these will be replaced with local read models.

- [ ] **Step 3: Write Program.cs**

Create `PaymentProcessing.Api/Program.cs`:
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PaymentProcessing.Api.Modules.PaymentProcessing.BinRecords;
using PaymentProcessing.Api.Modules.PaymentProcessing.Merchants;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Features.Endpoints;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Middleware;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ReadModels;
using Wolverine;
using Wolverine.Marten;
using Wolverine.RabbitMQ;
using SharedPaymentEvents = PaymentGateway.SharedContracts.PaymentEvents;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

var paymentDb = builder.Configuration.GetConnectionString("paymentDb")!;
var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
var jwtSecret = builder.Configuration["Jwt__SecretKey"]!;

// JWT validation — trusts tokens issued by ApiGateway
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt__Issuer"] ?? "payment-gateway",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt__Audience"] ?? "payment-gateway-services",
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddMarten(opts =>
{
    opts.Connection(paymentDb);
    opts.Projections.Snapshot<PaymentTransaction>(SnapshotLifecycle.Inline);
    opts.Schema.For<PaymentTransaction>()
        .UniqueIndex(UniqueIndexType.Computed, t => t.MerchantId, t => t.OrderId);
    opts.Schema.For<BinRecord>()
        .Index(b => b.BinEightStart)
        .Index(b => b.BinEightEnd);
    opts.Schema.For<MerchantSummary>();
    opts.Schema.For<BankRouteSummary>()
        .Index(r => r.MerchantId)
        .Index(r => r.Currency);
}).IntegrateWithWolverine(x => x.UseFastEventForwarding = true)
  .ApplyAllDatabaseChangesOnStartup();

builder.Services.AddGrpcClient<BankPaymentServiceClient>("garanti", o =>
    o.Address = new Uri(builder.Configuration.GetServiceUri("garanti")!.ToString()));

builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    // Subscribe to merchant data for local read model
    opts.ListenToRabbitQueue("payment-processing.merchant-events")
        .BindExchange("merchant.created")
        .BindExchange("merchant.updated")
        .BindExchange("merchant.status-changed");

    // Subscribe to bank routing data for local read model
    opts.ListenToRabbitQueue("payment-processing.bank-routes")
        .BindExchange("bank.route-synced");

    opts.PublishMessage<SharedPaymentEvents.PaymentApprovedIntegration>()
        .ToRabbitExchange("payment.approved");
    opts.PublishMessage<SharedPaymentEvents.PaymentDeclinedIntegration>()
        .ToRabbitExchange("payment.declined");
    opts.PublishMessage<SharedPaymentEvents.PaymentFailedIntegration>()
        .ToRabbitExchange("payment.failed");

    opts.Policies.AddMiddleware(typeof(MerchantMiddleware),
        chain => chain.MessageType.HasAttribute<RequiresMerchantAttribute>());
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapPaymentTransactionEndpoints();
app.Run();
```

- [ ] **Step 4: Commit**
```bash
git add PaymentProcessing.Api/
git commit -m "feat: setup PaymentProcessing.Api — Marten, JWT auth, Wolverine"
```

---

### Task 8: MerchantSummary and BankRouteSummary read models

**Files:**
- Create: `PaymentProcessing.Api/Modules/PaymentProcessing/Merchants/MerchantSummary.cs`
- Create: `PaymentProcessing.Api/Modules/PaymentProcessing/Merchants/MerchantEventHandlers.cs`
- Create: `PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/ReadModels/BankRouteSummary.cs`
- Create: `PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/ReadModels/BankRouteEventHandlers.cs`

- [ ] **Step 1: Create MerchantSummary document**

Create `PaymentProcessing.Api/Modules/PaymentProcessing/Merchants/MerchantSummary.cs`:
```csharp
namespace PaymentProcessing.Api.Modules.PaymentProcessing.Merchants;

public class MerchantSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }
    public string Status { get; set; } = "Active";
}
```

- [ ] **Step 2: Create MerchantSummary event handlers**

Create `PaymentProcessing.Api/Modules/PaymentProcessing/Merchants/MerchantEventHandlers.cs`:
```csharp
using Marten;
using PaymentGateway.SharedContracts.MerchantEvents;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.Merchants;

public static class MerchantEventHandlers
{
    public static async Task Handle(MerchantCreated evt, IDocumentSession session)
    {
        session.Store(new MerchantSummary
        {
            Id = evt.MerchantId,
            Name = evt.Name,
            Status = "Active"
        });
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantUpdated evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<MerchantSummary>(evt.MerchantId);
        if (summary is null) return;
        if (evt.Name is not null) summary.Name = evt.Name;
        if (evt.WebhookUrl is not null) summary.WebhookUrl = evt.WebhookUrl;
        session.Store(summary);
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantStatusChanged evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<MerchantSummary>(evt.MerchantId);
        if (summary is null) return;
        summary.Status = evt.NewStatus;
        session.Store(summary);
        await session.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Create BankRouteSummary document**

Create `PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/ReadModels/BankRouteSummary.cs`:
```csharp
namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ReadModels;

public class BankRouteSummary
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid BankId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal BankRate { get; set; }
    public decimal MerchantRate { get; set; }
}
```

- [ ] **Step 4: Create BankRouteSummary event handler**

Create `PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/ReadModels/BankRouteEventHandlers.cs`:
```csharp
using Marten;
using PaymentGateway.SharedContracts.BankIntegrationEvents;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ReadModels;

public static class BankRouteEventHandlers
{
    public static async Task Handle(BankRouteSynced evt, IDocumentSession session)
    {
        var existing = await session.Query<BankRouteSummary>()
            .Where(r => r.MerchantId == evt.MerchantId && r.Currency == evt.Currency)
            .FirstOrDefaultAsync();

        var route = existing ?? new BankRouteSummary { Id = Guid.NewGuid() };
        route.MerchantId = evt.MerchantId;
        route.BankId = evt.BankId;
        route.BankName = evt.BankName;
        route.Currency = evt.Currency;
        route.BankRate = evt.BankRate;
        route.MerchantRate = evt.MerchantRate;

        session.Store(route);
        await session.SaveChangesAsync();
    }
}
```

- [ ] **Step 5: Commit**
```bash
git add PaymentProcessing.Api/Modules/PaymentProcessing/Merchants/ PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/ReadModels/
git commit -m "feat: add MerchantSummary and BankRouteSummary read models with event handlers"
```

---

### Task 9: Refactor MerchantMiddleware and BankSelector

**Files:**
- Modify: `PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/Middleware/MerchantMiddleware.cs`
- Modify: `PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/Services/BankAdapters/Abstractions/IBankSelector.cs` (the BankSelector class inside it)

- [ ] **Step 1: Replace MerchantMiddleware with JWT claim reader**

Replace the entire content of `MerchantMiddleware.cs`:
```csharp
namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Middleware;

public class MerchantMiddleware
{
    public static (HandlerContinuation, MerchantIdentity) Before(
        IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var merchantIdClaim = user?.FindFirst("merchant_id")?.Value;
        var merchantNameClaim = user?.FindFirst("merchant_name")?.Value;

        if (string.IsNullOrEmpty(merchantIdClaim))
            throw new UnauthorizedAccessException("Merchant context missing from token.");

        return (HandlerContinuation.Continue, new MerchantIdentity(
            Guid.Parse(merchantIdClaim),
            merchantNameClaim ?? string.Empty));
    }
}
```

Note: Method is now synchronous (`Before` not `BeforeAsync`) — JWT claims are in-memory, no I/O needed.

- [ ] **Step 2: Rewrite BankSelector to use local Marten read model**

In `IBankSelector.cs`, replace the `BankSelector` implementation (keep the `IBankSelector` interface and `BankRoute` record unchanged):
```csharp
public class BankSelector(IDocumentSession session) : IBankSelector
{
    public async Task<BankRoute> SelectBestAsync(
        Guid merchantId, string currency, CardProfile cardProfile, CancellationToken ct)
    {
        var route = await session.Query<BankRouteSummary>()
            .Where(r => r.MerchantId == merchantId && r.Currency == currency)
            .FirstOrDefaultAsync(token: ct);

        if (route is null)
            throw new InvalidOperationException(
                $"No bank route found for merchant {merchantId}, currency {currency}. " +
                "Ensure BankIntegration has published a BankRouteSynced event.");

        return new BankRoute(route.BankId, route.BankName, route.BankRate, route.MerchantRate);
    }
}
```

Remove `BankIntegrationContext` and `CommissionManagementContext` constructor dependencies — they no longer exist in this service.

- [ ] **Step 3: Add using for BankRouteSummary to IBankSelector.cs**
```csharp
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ReadModels;
```

- [ ] **Step 4: Verify build**
```bash
dotnet build PaymentProcessing.Api/PaymentProcessing.Api.csproj
```

- [ ] **Step 5: Commit**
```bash
git add PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/
git commit -m "refactor: MerchantMiddleware reads JWT claims, BankSelector uses local Marten read model"
```

---

### Task 10: Publish integration events from AuthPayment handler

**Files:**
- Modify: `PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/Features/Commands/AuthPayment.cs`

- [ ] **Step 1: Return integration events from AuthPayment handler**

In `AuthPayment.cs`, update the `Handle` method to return payment integration events alongside the response. These trigger settlement and webhook dispatch in other services:

```csharp
using SharedPaymentEvents = PaymentGateway.SharedContracts.PaymentEvents;

// Change return type:
public async Task<(FeatureObjectResultModel<AuthPaymentResponse>, SharedPaymentEvents.PaymentApprovedIntegration?, SharedPaymentEvents.PaymentDeclinedIntegration?, SharedPaymentEvents.PaymentFailedIntegration?)> Handle(...)
{
    // ... existing logic ...

    // After PaymentApproved event appended:
    if (grpcResponse.IsApproved)
    {
        // ... existing approved logic ...
        await session.SaveChangesAsync(ct);
        var integrationEvent = new SharedPaymentEvents.PaymentApprovedIntegration(
            transactionId, merchant.MerchantId, cmd.OrderId, cmd.Amount, cmd.Currency,
            commission.MerchantAmount, DateTime.UtcNow);
        return (FeatureObjectResultModel<AuthPaymentResponse>.Ok(new AuthPaymentResponse { TransactionId = transactionId }),
                integrationEvent, null, null);
    }
    else
    {
        // ... existing declined logic ...
        await session.SaveChangesAsync(ct);
        var integrationEvent = new SharedPaymentEvents.PaymentDeclinedIntegration(
            transactionId, merchant.MerchantId, cmd.OrderId, grpcResponse.ResultCode, DateTime.UtcNow);
        return (FeatureObjectResultModel<AuthPaymentResponse>.Ok(new AuthPaymentResponse { TransactionId = transactionId }),
                null, integrationEvent, null);
    }

    // In catch RpcException:
    await session.SaveChangesAsync(ct);
    var failedEvent = new SharedPaymentEvents.PaymentFailedIntegration(
        transactionId, merchant.MerchantId, cmd.OrderId, ex.Status.Detail, DateTime.UtcNow);
    return (FeatureObjectResultModel<AuthPaymentResponse>.Ok(new AuthPaymentResponse { TransactionId = transactionId }),
            null, null, failedEvent);
}
```

- [ ] **Step 2: Verify build**
```bash
dotnet build PaymentProcessing.Api/PaymentProcessing.Api.csproj
```

- [ ] **Step 3: Commit**
```bash
git add PaymentProcessing.Api/Modules/PaymentProcessing/PaymentTransactions/Features/Commands/AuthPayment.cs
git commit -m "feat: publish payment integration events from AuthPayment handler"
```

---

## Phase 5: BankIntegration.Api

### Task 11: Setup BankIntegration.Api and publish BankRouteSynced

**Files:**
- Create: `BankIntegration.Api/Program.cs`
- Copy: `PaymentGatewayApi/Modules/BankIntegration/` → `BankIntegration.Api/Modules/BankIntegration/`
- Copy: `PaymentGatewayApi/Contexts/BankIntegrationContext.cs` → `BankIntegration.Api/BankIntegrationContext.cs`
- Create: `BankIntegration.Api/Modules/BankIntegration/Handlers/BankRouteSyncHandler.cs`

- [ ] **Step 1: Add NuGet packages**
```bash
dotnet add BankIntegration.Api/BankIntegration.Api.csproj package WolverineFx
dotnet add BankIntegration.Api/BankIntegration.Api.csproj package WolverineFx.Http
dotnet add BankIntegration.Api/BankIntegration.Api.csproj package WolverineFx.RabbitMQ
dotnet add BankIntegration.Api/BankIntegration.Api.csproj package WolverineFx.Postgresql
dotnet add BankIntegration.Api/BankIntegration.Api.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add BankIntegration.Api/BankIntegration.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add BankIntegration.Api/BankIntegration.Api.csproj reference Common/Common.csproj
```

- [ ] **Step 2: Copy module files and fix namespaces**
```bash
cp -r PaymentGatewayApi/Modules/BankIntegration BankIntegration.Api/Modules/BankIntegration
cp PaymentGatewayApi/Contexts/BankIntegrationContext.cs BankIntegration.Api/BankIntegrationContext.cs

find BankIntegration.Api/ -name "*.cs" -exec sed -i '' \
  's/PaymentGatewayApi\.Modules\.BankIntegration/BankIntegration.Api.Modules.BankIntegration/g;
   s/PaymentGatewayApi\.Contexts/BankIntegration.Api/g' {} \;
```

- [ ] **Step 3: Create BankRouteSyncHandler**

Whenever a MerchantBank is added/activated or a commission is set, BankIntegration needs to publish `BankRouteSynced`. The simplest approach: a dedicated command that recomputes and publishes the route.

Create `BankIntegration.Api/Modules/BankIntegration/Handlers/BankRouteSyncHandler.cs`:
```csharp
using BankIntegration.Api.Modules.BankIntegration.Banks;
using BankIntegration.Api.Modules.BankIntegration.MerchantBanks;
using PaymentGateway.SharedContracts.BankIntegrationEvents;

namespace BankIntegration.Api.Modules.BankIntegration.Handlers;

// This handler is called after any AddMerchantBankAccount or commission change.
// It publishes BankRouteSynced so PaymentProcessing can update its local read model.
public static class BankRouteSyncHandler
{
    public record SyncBankRoute(Guid MerchantId, string Currency);

    public static async Task<BankRouteSynced?> Handle(
        SyncBankRoute cmd,
        BankIntegrationContext db,
        CancellationToken ct)
    {
        var merchantBank = await db.Set<MerchantBank>()
            .Include(mb => mb.Bank)
            .Where(mb => mb.MerchantId == cmd.MerchantId
                         && mb.Bank.SupportsCurrency(cmd.Currency)
                         && mb.Status == MerchantBankStatus.Active)
            .FirstOrDefaultAsync(ct);

        if (merchantBank is null) return null;

        // BankRate and MerchantRate come from CommissionManagement.
        // In the microservices version, CommissionManagement publishes these via a separate event.
        // For now, use placeholder rates — wire up properly when CommissionManagement.Api is set up.
        return new BankRouteSynced(
            cmd.MerchantId,
            merchantBank.BankId,
            merchantBank.Bank.Name.Value,
            cmd.Currency,
            BankRate: 0,       // TODO: receive from CommissionManagement via event
            MerchantRate: 0,   // TODO: receive from CommissionManagement via event
            DateTime.UtcNow);
    }
}
```

- [ ] **Step 4: Write Program.cs**

Create `BankIntegration.Api/Program.cs`:
```csharp
using BankIntegration.Api.Modules.BankIntegration.Banks.Features.Endpoints;
using BankIntegration.Api.Modules.BankIntegration.MerchantBanks.Features.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.RabbitMQ;
using SharedBankEvents = PaymentGateway.SharedContracts.BankIntegrationEvents;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

var connString = builder.Configuration.GetConnectionString("bankIntegrationDb")!;
var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
var jwtSecret = builder.Configuration["Jwt__SecretKey"]!;

builder.Services.AddDbContextWithWolverineIntegration<BankIntegrationContext>(
    opts => opts.UseNpgsql(connString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts => opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true, ValidIssuer = "payment-gateway",
        ValidateAudience = true, ValidAudience = "payment-gateway-services",
        ValidateLifetime = true
    });
builder.Services.AddAuthorization();

builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    opts.PublishMessage<SharedBankEvents.BankRouteSynced>().ToRabbitExchange("bank.route-synced");
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapBankEndpoints();
app.MapMerchantBankEndpoints();
app.Run();
```

- [ ] **Step 5: Verify build**
```bash
dotnet build BankIntegration.Api/BankIntegration.Api.csproj
```

- [ ] **Step 6: Commit**
```bash
git add BankIntegration.Api/
git commit -m "feat: setup BankIntegration.Api with BankRouteSynced event publishing"
```

---

## Phase 6: Remaining Services (CommissionManagement, IAM, Settlement)

These follow the identical pattern as BankIntegration.Api. Each step is the same — copy module, fix namespaces, write Program.cs, build, commit.

### Task 12: CommissionManagement.Api

**Files:**
- Create: `CommissionManagement.Api/Program.cs`
- Copy: `PaymentGatewayApi/Modules/CommissionManagement/` → `CommissionManagement.Api/Modules/CommissionManagement/`
- Copy: `PaymentGatewayApi/Contexts/CommissionManagementContext.cs` → `CommissionManagement.Api/CommissionManagementContext.cs`

- [ ] **Step 1: Add packages**
```bash
dotnet add CommissionManagement.Api/CommissionManagement.Api.csproj package WolverineFx
dotnet add CommissionManagement.Api/CommissionManagement.Api.csproj package WolverineFx.Http
dotnet add CommissionManagement.Api/CommissionManagement.Api.csproj package WolverineFx.RabbitMQ
dotnet add CommissionManagement.Api/CommissionManagement.Api.csproj package WolverineFx.Postgresql
dotnet add CommissionManagement.Api/CommissionManagement.Api.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add CommissionManagement.Api/CommissionManagement.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add CommissionManagement.Api/CommissionManagement.Api.csproj reference Common/Common.csproj
```

- [ ] **Step 2: Copy and fix namespaces**
```bash
cp -r PaymentGatewayApi/Modules/CommissionManagement CommissionManagement.Api/Modules/CommissionManagement
cp PaymentGatewayApi/Contexts/CommissionManagementContext.cs CommissionManagement.Api/CommissionManagementContext.cs

find CommissionManagement.Api/ -name "*.cs" -exec sed -i '' \
  's/PaymentGatewayApi\.Modules\.CommissionManagement/CommissionManagement.Api.Modules.CommissionManagement/g;
   s/PaymentGatewayApi\.Contexts/CommissionManagement.Api/g' {} \;
```

- [ ] **Step 3: Write Program.cs**

Create `CommissionManagement.Api/Program.cs`:
```csharp
using CommissionManagement.Api.Modules.CommissionManagement.BankCommissions.Features.Endpoints;
using CommissionManagement.Api.Modules.CommissionManagement.MerchantCommissions.Features.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

var connString = builder.Configuration.GetConnectionString("commissionDb")!;
var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
var jwtSecret = builder.Configuration["Jwt__SecretKey"]!;

builder.Services.AddDbContextWithWolverineIntegration<CommissionManagementContext>(
    opts => opts.UseNpgsql(connString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts => opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true, ValidIssuer = "payment-gateway",
        ValidateAudience = true, ValidAudience = "payment-gateway-services",
        ValidateLifetime = true
    });
builder.Services.AddAuthorization();

builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapBankCommissionEndpoints();
app.MapMerchantCommissionEndpoints();
app.Run();
```

- [ ] **Step 4: Build and commit**
```bash
dotnet build CommissionManagement.Api/CommissionManagement.Api.csproj
git add CommissionManagement.Api/
git commit -m "feat: setup CommissionManagement.Api microservice"
```

---

### Task 13: IAM.Api

**Files:**
- Create: `IAM.Api/Program.cs`
- Copy: `PaymentGatewayApi/Modules/IAM/` → `IAM.Api/Modules/IAM/`
- Copy: `PaymentGatewayApi/Contexts/IamContext.cs` → `IAM.Api/IamContext.cs`

- [ ] **Step 1: Add packages**
```bash
dotnet add IAM.Api/IAM.Api.csproj package WolverineFx
dotnet add IAM.Api/IAM.Api.csproj package WolverineFx.Http
dotnet add IAM.Api/IAM.Api.csproj package WolverineFx.RabbitMQ
dotnet add IAM.Api/IAM.Api.csproj package WolverineFx.Postgresql
dotnet add IAM.Api/IAM.Api.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add IAM.Api/IAM.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add IAM.Api/IAM.Api.csproj reference Common/Common.csproj
```

- [ ] **Step 2: Copy and fix namespaces**
```bash
cp -r PaymentGatewayApi/Modules/IAM IAM.Api/Modules/IAM
cp PaymentGatewayApi/Contexts/IamContext.cs IAM.Api/IamContext.cs

find IAM.Api/ -name "*.cs" -exec sed -i '' \
  's/PaymentGatewayApi\.Modules\.IAM/IAM.Api.Modules.IAM/g;
   s/PaymentGatewayApi\.Contexts/IAM.Api/g' {} \;
```

- [ ] **Step 3: Write Program.cs**

Create `IAM.Api/Program.cs`:
```csharp
using IAM.Api.Modules.IAM.Roles.Features.Endpoints;
using IAM.Api.Modules.IAM.Users.Features.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

var connString = builder.Configuration.GetConnectionString("iamDb")!;
var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
var jwtSecret = builder.Configuration["Jwt__SecretKey"]!;

builder.Services.AddDbContextWithWolverineIntegration<IamContext>(
    opts => opts.UseNpgsql(connString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts => opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true, ValidIssuer = "payment-gateway",
        ValidateAudience = true, ValidAudience = "payment-gateway-services",
        ValidateLifetime = true
    });
builder.Services.AddAuthorization();

builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapRoleEndpoints();
app.MapUserEndpoints();
app.Run();
```

- [ ] **Step 4: Build and commit**
```bash
dotnet build IAM.Api/IAM.Api.csproj
git add IAM.Api/
git commit -m "feat: setup IAM.Api microservice"
```

---

### Task 14: Settlement.Api

**Files:**
- Create: `Settlement.Api/Program.cs`
- Copy: `PaymentGatewayApi/Modules/Settlement/` → `Settlement.Api/Modules/Settlement/`
- Copy: `PaymentGatewayApi/Contexts/SettlementContext.cs` → `Settlement.Api/SettlementContext.cs`

- [ ] **Step 1: Add packages**
```bash
dotnet add Settlement.Api/Settlement.Api.csproj package WolverineFx
dotnet add Settlement.Api/Settlement.Api.csproj package WolverineFx.Http
dotnet add Settlement.Api/Settlement.Api.csproj package WolverineFx.RabbitMQ
dotnet add Settlement.Api/Settlement.Api.csproj package WolverineFx.Postgresql
dotnet add Settlement.Api/Settlement.Api.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add Settlement.Api/Settlement.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add Settlement.Api/Settlement.Api.csproj reference Common/Common.csproj
```

- [ ] **Step 2: Copy and fix namespaces**
```bash
cp -r PaymentGatewayApi/Modules/Settlement Settlement.Api/Modules/Settlement
cp PaymentGatewayApi/Contexts/SettlementContext.cs Settlement.Api/SettlementContext.cs

find Settlement.Api/ -name "*.cs" -exec sed -i '' \
  's/PaymentGatewayApi\.Modules\.Settlement/Settlement.Api.Modules.Settlement/g;
   s/PaymentGatewayApi\.Contexts/Settlement.Api/g' {} \;
```

- [ ] **Step 3: Write Program.cs**

Create `Settlement.Api/Program.cs`:
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Settlement.Api.Modules.Settlement.MerchantBalances.Features.Endpoints;
using Settlement.Api.Modules.Settlement.Settlements.Features.Endpoints;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

var connString = builder.Configuration.GetConnectionString("settlementDb")!;
var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
var jwtSecret = builder.Configuration["Jwt__SecretKey"]!;

builder.Services.AddDbContextWithWolverineIntegration<SettlementContext>(
    opts => opts.UseNpgsql(connString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts => opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true, ValidIssuer = "payment-gateway",
        ValidateAudience = true, ValidAudience = "payment-gateway-services",
        ValidateLifetime = true
    });
builder.Services.AddAuthorization();

builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    // Settlement triggers on payment approved events
    opts.ListenToRabbitQueue("settlement.payment-approved")
        .BindExchange("payment.approved");
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapSettlementEndpoints();
app.MapMerchantBalanceEndpoints();
app.Run();
```

- [ ] **Step 4: Build and commit**
```bash
dotnet build Settlement.Api/Settlement.Api.csproj
git add Settlement.Api/
git commit -m "feat: setup Settlement.Api — subscribes to payment.approved for settlement trigger"
```

---

## Phase 7: Update BFF

### Task 15: Route BFF through ApiGateway

**Files:**
- Modify: `PaymentGatewayBff/` — wherever the API base URL is configured

The BFF currently points to `PaymentGatewayApi`. It must point to `ApiGateway`. The BFF sends `X-Api-Key`; the Gateway exchanges it for a JWT transparently.

- [ ] **Step 1: Find current API URL configuration**
```bash
grep -r "payment-gateway-api\|PaymentGatewayApi\|GetServiceUri" PaymentGatewayBff/ --include="*.cs" --include="*.json"
```

- [ ] **Step 2: Update service URL to ApiGateway**

In wherever the BFF's `HttpClient` base address is configured, change the Aspire service name from `"payment-gateway-api"` to `"api-gateway"`:
```csharp
// Before:
client.BaseAddress = new Uri(builder.Configuration.GetServiceUri("payment-gateway-api")!.ToString());

// After:
client.BaseAddress = new Uri(builder.Configuration.GetServiceUri("api-gateway")!.ToString());
```

- [ ] **Step 3: Commit**
```bash
git add PaymentGatewayBff/
git commit -m "feat: route BFF through ApiGateway"
```

---

## Important Implementation Notes

### Namespace substitution
The `sed` commands in each task do a best-effort namespace rename. After running them, always do:
```bash
dotnet build <Service>/<Service>.csproj 2>&1 | grep "error CS"
```
Fix any remaining namespace errors manually.

### EF Core migrations per service
After each service builds successfully, create its initial migration:
```bash
dotnet ef migrations add InitialCreate --project MerchantManagement.Api
dotnet ef migrations add InitialCreate --project BankIntegration.Api
dotnet ef migrations add InitialCreate --project CommissionManagement.Api
dotnet ef migrations add InitialCreate --project IAM.Api
dotnet ef migrations add InitialCreate --project Settlement.Api
```
Marten (`paymentDb`) creates its schema automatically via `ApplyAllDatabaseChangesOnStartup()`.

### Commission rates in BankSelector (known gap)
`BankRouteSyncHandler` in Task 11 uses placeholder `BankRate: 0` and `MerchantRate: 0` because commission data lives in CommissionManagement. To wire this properly:
1. CommissionManagement publishes a `CommissionRateSet` integration event when rates change
2. BankIntegration subscribes, updates its local rate cache, then re-publishes `BankRouteSynced` with real rates
3. Or: add `CommissionManagementContext` as a reference to BankIntegration (acceptable as they're co-located in the same service boundary in the interim)

### Old PaymentGatewayApi
Keep it running during migration for reference. Once all services are verified in Aspire, remove it from AppHost and eventually delete it.

### JWT secret in Aspire
The `jwt-secret` parameter is stored in user secrets (AppHost project). In production, use Azure Key Vault or a similar secret store.