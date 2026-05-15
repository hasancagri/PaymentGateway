# YARP Gateway Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gateway.Api projesi oluşturmak: YARP routing, Keycloak JWT doğrulama, API Key doğrulama, merkezi cache ve event-driven invalidation.

**Architecture:** Gateway tek giriş noktasıdır. `/payments/**` için `ApiKeyMiddleware` (Redis cache + gRPC fallback + X-Merchant-* header inject), diğer tüm rotalar için `KeycloakAuthMiddleware` (JWT doğrulama + X-User-* header inject). Downstream servisler header'lara güvenir, kendi JWT doğrulamasını kaldırır.

**Tech Stack:** .NET 10, Yarp.ReverseProxy, Wolverine + RabbitMQ, StackExchange.Redis, Common/Caching, Keycloak JWT (gateway'de), gRPC (MerchantManagement API key lookup).

---

## Dosya Haritası

**Yeni dosyalar:**
- `Gateway.Api/Gateway.Api.csproj`
- `Gateway.Api/GlobalUsings.cs`
- `Gateway.Api/appsettings.json`
- `Gateway.Api/Program.cs`
- `Gateway.Api/Dependencies/DependencyExtensions.cs`
- `Gateway.Api/Models/MerchantCacheEntry.cs`
- `Gateway.Api/Cache/IApiKeyCacheService.cs`
- `Gateway.Api/Cache/ApiKeyCacheService.cs`
- `Gateway.Api/Middleware/KeycloakAuthMiddleware.cs`
- `Gateway.Api/Middleware/ApiKeyMiddleware.cs`
- `Gateway.Api/EventHandlers/ApiKeyRevokedHandler.cs`
- `Gateway.Api/EventHandlers/MerchantStatusChangedHandler.cs`
- `Gateway.Api.Tests/Gateway.Api.Tests.csproj`
- `Gateway.Api.Tests/Cache/ApiKeyCacheServiceTests.cs`
- `Gateway.Api.Tests/Middleware/ApiKeyMiddlewareTests.cs`
- `Gateway.Api.Tests/Middleware/KeycloakAuthMiddlewareTests.cs`

**Değiştirilen dosyalar:**
- `PaymentGateway.SharedContracts/IntegrationEvents.cs` — 2 yeni event
- `PaymentGateway.SharedContracts/ExchangeConstants.cs` — 2 yeni exchange key
- `PaymentGateway.SyncContracts/Protos/sync_merchant.proto` — `GetMerchantByApiKey` metodu
- `MerchantManagement.Api/Grpc/SyncMerchantGrpcService.cs` — yeni metot impl
- `MerchantManagement.Api/Domains/Merchants/Features/Commands/RevokeApiKey.cs` — event publish
- `MerchantManagement.Api/Domains/Merchants/Features/Commands/SuspendMerchant.cs` — event publish
- `MerchantManagement.Api/Domains/Merchants/Features/Commands/DeactivateMerchant.cs` — event publish
- `MerchantManagement.Api/Domains/Merchants/Features/Commands/ActivateMerchant.cs` — event publish
- `MerchantManagement.Api/Program.cs` — RabbitMQ wiring ekle
- `ServiceDefaults/Extensions.cs` — `AddGatewayIdentity()` ekle
- `AppHost/AppHost.cs` — Gateway.Api ekle
- `PaymentGateway.slnx` — Gateway projeleri ekle
- Her downstream servis `Program.cs` × 6 — `AddKeycloakJwtAuthentication()` → `AddGatewayIdentity()`
- Her downstream servis `Auths/AuthExtensions.cs` × 5 — header'dan okuyacak şekilde güncelle
- `PaymentProcessing.Api/Domains/PaymentTransactions/Middleware/MerchantMiddleware.cs` — header'dan okuyacak şekilde güncelle

---

## Task 1: SharedContracts — Yeni integration events ve exchange constants

**Files:**
- Modify: `PaymentGateway.SharedContracts/IntegrationEvents.cs`
- Modify: `PaymentGateway.SharedContracts/ExchangeConstants.cs`

- [ ] **Step 1: IntegrationEvents.cs dosyasına iki yeni event ekle**

`PaymentGateway.SharedContracts/IntegrationEvents.cs` sonuna ekle:

```csharp
public sealed record ApiKeyRevoked(
    string ApiKeyHash,
    Guid MerchantId,
    DateTime OccurredOn);

public sealed record MerchantStatusChanged(
    Guid MerchantId,
    MerchantStatus NewStatus,
    IReadOnlyList<string> ApiKeyHashes,
    DateTime OccurredOn);
```

- [ ] **Step 2: ExchangeConstants.cs'e yeni sabitler ekle**

```csharp
public const string ApiKeyRevoked = "merchant.api-key-revoked";
public const string MerchantStatusChanged = "merchant.merchant-status-changed";
```

- [ ] **Step 3: Build ile derle**

```bash
dotnet build PaymentGateway.SharedContracts/PaymentGateway.SharedContracts.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 4: Commit**

```bash
git add PaymentGateway.SharedContracts/IntegrationEvents.cs PaymentGateway.SharedContracts/ExchangeConstants.cs
git commit -m "feat: add ApiKeyRevoked and MerchantStatusChanged integration events"
```

---

## Task 2: SyncContracts — Proto'ya GetMerchantByApiKey ekle

**Files:**
- Modify: `PaymentGateway.SyncContracts/Protos/sync_merchant.proto`

- [ ] **Step 1: sync_merchant.proto'ya yeni RPC ve mesajlar ekle**

`SyncMerchantService` service bloğuna ekle:

```proto
rpc GetMerchantByApiKey (ApiKeyRequest) returns (MerchantApiKeyResponse);
```

Dosyanın sonuna yeni mesaj tanımlarını ekle:

```proto
message ApiKeyRequest {
    string key_hash = 1;
}

message MerchantApiKeyResponse {
    bool found = 1;
    string merchant_id = 2;
    string merchant_name = 3;
    int32 merchant_status = 4;
    int32 key_status = 5;
}
```

- [ ] **Step 2: SyncContracts build ile derle ve gRPC dosyalarının üretildiğini doğrula**

```bash
dotnet build PaymentGateway.SyncContracts/PaymentGateway.SyncContracts.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add PaymentGateway.SyncContracts/Protos/sync_merchant.proto
git commit -m "feat: add GetMerchantByApiKey to sync_merchant proto"
```

---

## Task 3: MerchantManagement — GetMerchantByApiKey gRPC impl

**Files:**
- Modify: `MerchantManagement.Api/Grpc/SyncMerchantGrpcService.cs`

- [ ] **Step 1: SyncMerchantGrpcService.cs'e yeni metot ekle**

Mevcut sınıfa `GetMerchantByApiKey` override'ı ekle:

```csharp
public override async Task<MerchantApiKeyResponse> GetMerchantByApiKey(
    ApiKeyRequest request, ServerCallContext context)
{
    var merchant = await session.Query<Merchant>()
        .FirstOrDefaultAsync(
            m => m.ApiKeys.Any(k => k.KeyValue.Hash == request.KeyHash),
            context.CancellationToken);

    if (merchant is null)
        return new MerchantApiKeyResponse { Found = false };

    var key = merchant.ApiKeys.FirstOrDefault(k => k.KeyValue.Hash == request.KeyHash);
    if (key is null)
        return new MerchantApiKeyResponse { Found = false };

    return new MerchantApiKeyResponse
    {
        Found = true,
        MerchantId = merchant.Id.ToString(),
        MerchantName = merchant.Name.Value,
        MerchantStatus = (int)merchant.Status,
        KeyStatus = (int)key.Status
    };
}
```

- [ ] **Step 2: Build ile derle**

```bash
dotnet build MerchantManagement.Api/MerchantManagement.Api.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add MerchantManagement.Api/Grpc/SyncMerchantGrpcService.cs
git commit -m "feat: implement GetMerchantByApiKey gRPC method"
```

---

## Task 4: MerchantManagement — Yeni events publish + RabbitMQ wiring

**Files:**
- Modify: `MerchantManagement.Api/Domains/Merchants/Features/Commands/RevokeApiKey.cs`
- Modify: `MerchantManagement.Api/Domains/Merchants/Features/Commands/SuspendMerchant.cs`
- Modify: `MerchantManagement.Api/Domains/Merchants/Features/Commands/DeactivateMerchant.cs`
- Modify: `MerchantManagement.Api/Domains/Merchants/Features/Commands/ActivateMerchant.cs`
- Modify: `MerchantManagement.Api/Program.cs`

- [ ] **Step 1: RevokeApiKeyHandler'a IMessageBus ekle ve ApiKeyRevoked publish et**

`RevokeApiKeyHandler.Handle` imzasını güncelle:

```csharp
public async Task<FeatureObjectResultModel<RevokeApiKeyCommandResponse>> Handle(
    RevokeApiKeyCommand cmd,
    IDocumentSession session,
    IMessageBus bus,
    CancellationToken ct)
```

`session.Store(merchant);` satırından sonra ekle:

```csharp
await bus.PublishAsync(new ApiKeyRevoked(
    ApiKeyHash: keyToRevoke.KeyValue.Hash,
    MerchantId: cmd.MerchantId,
    OccurredOn: DateTime.UtcNow));
```

- [ ] **Step 2: SuspendMerchantHandler'a MerchantStatusChanged publish et**

`SuspendMerchantHandler.Handle` içinde `await bus.PublishAsync(new MerchantSynced(...))` satırından sonra ekle:

```csharp
await bus.PublishAsync(new MerchantStatusChanged(
    MerchantId: merchant.Id,
    NewStatus: MerchantStatus.Suspended,
    ApiKeyHashes: merchant.ApiKeys.Select(k => k.KeyValue.Hash).ToList(),
    OccurredOn: DateTime.UtcNow));
```

- [ ] **Step 3: DeactivateMerchantHandler'a MerchantStatusChanged publish et**

Aynı şekilde `DeactivateMerchant.cs` içinde `await bus.PublishAsync(new MerchantSynced(...))` satırından sonra ekle:

```csharp
await bus.PublishAsync(new MerchantStatusChanged(
    MerchantId: merchant.Id,
    NewStatus: MerchantStatus.Passive,
    ApiKeyHashes: merchant.ApiKeys.Select(k => k.KeyValue.Hash).ToList(),
    OccurredOn: DateTime.UtcNow));
```

- [ ] **Step 4: ActivateMerchantHandler'a MerchantStatusChanged publish et**

Aynı şekilde `ActivateMerchant.cs` içinde `await bus.PublishAsync(new MerchantSynced(...))` satırından sonra ekle:

```csharp
await bus.PublishAsync(new MerchantStatusChanged(
    MerchantId: merchant.Id,
    NewStatus: MerchantStatus.Active,
    ApiKeyHashes: merchant.ApiKeys.Select(k => k.KeyValue.Hash).ToList(),
    OccurredOn: DateTime.UtcNow));
```

- [ ] **Step 5: MerchantManagement Program.cs'e RabbitMQ wiring ekle**

Mevcut `builder.Host.UseWolverine(opts => { ... });` bloğunu şu şekilde güncelle:

```csharp
var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

    var transport = opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();
    transport.DeclareExchange(ExchangeConstants.ApiKeyRevoked,
        e => e.ExchangeType = ExchangeType.Fanout);
    transport.DeclareExchange(ExchangeConstants.MerchantStatusChanged,
        e => e.ExchangeType = ExchangeType.Fanout);

    opts.PublishMessage<ApiKeyRevoked>()
        .ToRabbitExchange(ExchangeConstants.ApiKeyRevoked);
    opts.PublishMessage<MerchantStatusChanged>()
        .ToRabbitExchange(ExchangeConstants.MerchantStatusChanged);
});
```

`using RabbitMQ.Client;` using'i eklendiğinden emin ol (Wolverine.RabbitMQ paketi sağlar).

- [ ] **Step 6: Build**

```bash
dotnet build MerchantManagement.Api/MerchantManagement.Api.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 7: Commit**

```bash
git add MerchantManagement.Api/
git commit -m "feat: publish ApiKeyRevoked and MerchantStatusChanged from MerchantManagement"
```

---

## Task 5: Gateway.Api projesi oluştur

**Files:**
- Create: `Gateway.Api/Gateway.Api.csproj`
- Create: `Gateway.Api/GlobalUsings.cs`
- Create: `Gateway.Api/appsettings.json`
- Modify: `PaymentGateway.slnx`

- [ ] **Step 1: Gateway.Api.csproj oluştur**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Yarp.ReverseProxy" Version="2.3.0" />
    <PackageReference Include="WolverineFx" Version="5.38.0" />
    <PackageReference Include="WolverineFx.RabbitMQ" Version="5.38.0" />
    <PackageReference Include="StackExchange.Redis" Version="2.8.16" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ServiceDefaults\ServiceDefaults.csproj" />
    <ProjectReference Include="..\Common\Common.csproj" />
    <ProjectReference Include="..\PaymentGateway.SharedContracts\PaymentGateway.SharedContracts.csproj" />
    <ProjectReference Include="..\PaymentGateway.SyncContracts\PaymentGateway.SyncContracts.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: GlobalUsings.cs oluştur**

```csharp
global using Common.Auths;
global using Common.Caching;
global using PaymentGateway.SharedContracts;
```

- [ ] **Step 3: appsettings.json oluştur (YARP konfigürasyonu)**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Yarp": "Information"
    }
  },
  "Keycloak": {
    "Authority": "",
    "AdminApiBaseUrl": "",
    "Realm": "payment-gateway",
    "ClientId": "payment-api",
    "ClientSecret": ""
  },
  "ReverseProxy": {
    "Routes": {
      "payments-route": {
        "ClusterId": "payment-processing",
        "Match": { "Path": "/payments/{**catch-all}" },
        "Metadata": { "AuthType": "ApiKey" }
      },
      "auth-route": {
        "ClusterId": "iam",
        "Match": { "Path": "/auth/{**catch-all}" },
        "Metadata": { "AuthType": "Keycloak" }
      },
      "merchants-route": {
        "ClusterId": "merchant-management",
        "Match": { "Path": "/merchants/{**catch-all}" },
        "Metadata": { "AuthType": "Keycloak" }
      },
      "banks-route": {
        "ClusterId": "bank-integration",
        "Match": { "Path": "/banks/{**catch-all}" },
        "Metadata": { "AuthType": "Keycloak" }
      },
      "commissions-route": {
        "ClusterId": "commission-management",
        "Match": { "Path": "/commissions/{**catch-all}" },
        "Metadata": { "AuthType": "Keycloak" }
      },
      "settlements-route": {
        "ClusterId": "settlement",
        "Match": { "Path": "/settlements/{**catch-all}" },
        "Metadata": { "AuthType": "Keycloak" }
      }
    },
    "Clusters": {
      "payment-processing": {
        "Destinations": { "primary": { "Address": "http://payment-processing" } }
      },
      "iam": {
        "Destinations": { "primary": { "Address": "http://iam" } }
      },
      "merchant-management": {
        "Destinations": { "primary": { "Address": "http://merchant-management" } }
      },
      "bank-integration": {
        "Destinations": { "primary": { "Address": "http://bank-integration" } }
      },
      "commission-management": {
        "Destinations": { "primary": { "Address": "http://commission-management" } }
      },
      "settlement": {
        "Destinations": { "primary": { "Address": "http://settlement" } }
      }
    }
  }
}
```

- [ ] **Step 4: PaymentGateway.slnx'e Gateway projelerini ekle**

`/Services/` klasörüne ekle:

```xml
<Project Path="Gateway.Api/Gateway.Api.csproj" />
```

`/AspireProjects/` altına ekle (ilerleyen task'ta test projesi de eklenecek):

```xml
<Project Path="Gateway.Api.Tests/Gateway.Api.Tests.csproj" />
```

- [ ] **Step 5: Build**

```bash
dotnet build Gateway.Api/Gateway.Api.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 6: Commit**

```bash
git add Gateway.Api/ PaymentGateway.slnx
git commit -m "feat: scaffold Gateway.Api project with YARP config"
```

---

## Task 6: Gateway.Api.Tests projesi oluştur

**Files:**
- Create: `Gateway.Api.Tests/Gateway.Api.Tests.csproj`

- [ ] **Step 1: Gateway.Api.Tests.csproj oluştur**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Gateway.Api\Gateway.Api.csproj" />
    <ProjectReference Include="..\PaymentGateway.SharedContracts\PaymentGateway.SharedContracts.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Build**

```bash
dotnet build Gateway.Api.Tests/Gateway.Api.Tests.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add Gateway.Api.Tests/
git commit -m "feat: add Gateway.Api.Tests project"
```

---

## Task 7: MerchantCacheEntry modeli ve ApiKeyCacheService (TDD)

**Files:**
- Create: `Gateway.Api/Models/MerchantCacheEntry.cs`
- Create: `Gateway.Api/Cache/IApiKeyCacheService.cs`
- Create: `Gateway.Api/Cache/ApiKeyCacheService.cs`
- Create: `Gateway.Api.Tests/Cache/ApiKeyCacheServiceTests.cs`

- [ ] **Step 1: MerchantCacheEntry modeli oluştur**

`Gateway.Api/Models/MerchantCacheEntry.cs`:

```csharp
namespace Gateway.Api.Models;

public sealed record MerchantCacheEntry(
    Guid MerchantId,
    string MerchantName,
    MerchantStatus MerchantStatus,
    ApiKeyStatus ApiKeyStatus);
```

- [ ] **Step 2: IApiKeyCacheService interface oluştur**

`Gateway.Api/Cache/IApiKeyCacheService.cs`:

```csharp
using Gateway.Api.Models;

namespace Gateway.Api.Cache;

public interface IApiKeyCacheService
{
    Task<MerchantCacheEntry?> GetAsync(string keyHash, CancellationToken ct = default);
    Task SetAsync(string keyHash, MerchantCacheEntry entry, CancellationToken ct = default);
    Task RemoveAsync(string keyHash, CancellationToken ct = default);
}
```

- [ ] **Step 3: Failing testleri yaz**

`Gateway.Api.Tests/Cache/ApiKeyCacheServiceTests.cs`:

```csharp
using Gateway.Api.Cache;
using Gateway.Api.Models;
using Moq;
using PaymentGateway.SharedContracts;
using Xunit;
using Common.Caching;

namespace Gateway.Api.Tests.Cache;

public class ApiKeyCacheServiceTests
{
    private readonly Mock<ICacheManager> _cacheMock = new();
    private readonly ApiKeyCacheService _sut;

    public ApiKeyCacheServiceTests()
    {
        _sut = new ApiKeyCacheService(_cacheMock.Object);
    }

    [Fact]
    public async Task GetAsync_WhenCacheHit_ReturnsCachedEntry()
    {
        var hash = "abc123";
        var entry = new MerchantCacheEntry(Guid.NewGuid(), "TestMerchant", MerchantStatus.Active, ApiKeyStatus.Active);
        _cacheMock.Setup(x => x.Get<MerchantCacheEntry>($"apikey:{hash}"))
            .ReturnsAsync(entry);

        var result = await _sut.GetAsync(hash);

        Assert.NotNull(result);
        Assert.Equal(entry.MerchantId, result.MerchantId);
    }

    [Fact]
    public async Task GetAsync_WhenCacheMiss_ReturnsNull()
    {
        _cacheMock.Setup(x => x.Get<MerchantCacheEntry>(It.IsAny<string>()))
            .ReturnsAsync((MerchantCacheEntry?)null);

        var result = await _sut.GetAsync("notfound");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_StoresEntryWithCorrectKey()
    {
        var hash = "abc123";
        var entry = new MerchantCacheEntry(Guid.NewGuid(), "TestMerchant", MerchantStatus.Active, ApiKeyStatus.Active);

        await _sut.SetAsync(hash, entry);

        _cacheMock.Verify(x => x.Set($"apikey:{hash}", entry), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_RemovesEntryWithCorrectKey()
    {
        var hash = "abc123";

        await _sut.RemoveAsync(hash);

        _cacheMock.Verify(x => x.Remove($"apikey:{hash}"), Times.Once);
    }
}
```

- [ ] **Step 4: Test çalıştır — fail bekleniyor**

```bash
dotnet test Gateway.Api.Tests/Gateway.Api.Tests.csproj --filter "ApiKeyCacheServiceTests"
```

Expected: FAIL — ApiKeyCacheService does not exist

- [ ] **Step 5: ApiKeyCacheService implementasyonunu yaz**

`Gateway.Api/Cache/ApiKeyCacheService.cs`:

```csharp
using Common.Caching;
using Common.Dependencies.Models;
using Gateway.Api.Models;

namespace Gateway.Api.Cache;

public sealed class ApiKeyCacheService(ICacheManager cache) : IApiKeyCacheService, ITransientDependency
{
    private static string Key(string hash) => $"apikey:{hash}";

    public Task<MerchantCacheEntry?> GetAsync(string keyHash, CancellationToken ct = default)
        => cache.Get<MerchantCacheEntry>(Key(keyHash));

    public Task SetAsync(string keyHash, MerchantCacheEntry entry, CancellationToken ct = default)
        => cache.Set(Key(keyHash), entry);

    public Task RemoveAsync(string keyHash, CancellationToken ct = default)
        => cache.Remove(Key(keyHash));
}
```

- [ ] **Step 6: Test çalıştır — pass bekleniyor**

```bash
dotnet test Gateway.Api.Tests/Gateway.Api.Tests.csproj --filter "ApiKeyCacheServiceTests"
```

Expected: 4 passed, 0 failed

- [ ] **Step 7: Commit**

```bash
git add Gateway.Api/Models/ Gateway.Api/Cache/ Gateway.Api.Tests/Cache/
git commit -m "feat: add MerchantCacheEntry and ApiKeyCacheService with tests"
```

---

## Task 8: KeycloakAuthMiddleware (TDD)

**Files:**
- Create: `Gateway.Api/Middleware/KeycloakAuthMiddleware.cs`
- Create: `Gateway.Api.Tests/Middleware/KeycloakAuthMiddlewareTests.cs`

- [ ] **Step 1: Failing testleri yaz**

`Gateway.Api.Tests/Middleware/KeycloakAuthMiddlewareTests.cs`:

```csharp
using Gateway.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Gateway.Api.Tests.Middleware;

public class KeycloakAuthMiddlewareTests
{
    private static HttpContext BuildContext(string path, bool isAuthenticated, string? sub = null, string? email = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        if (isAuthenticated)
        {
            var claims = new List<System.Security.Claims.Claim>();
            if (sub is not null) claims.Add(new("sub", sub));
            if (email is not null) claims.Add(new("email", email));
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestScheme");
            context.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }

        return context;
    }

    [Fact]
    public async Task NonPaymentPath_WithoutAuth_Returns401()
    {
        var context = BuildContext("/merchants/123", isAuthenticated: false);
        var nextCalled = false;
        var middleware = new KeycloakAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task NonPaymentPath_WithAuth_InjectsHeadersAndCallsNext()
    {
        var context = BuildContext("/merchants/123", isAuthenticated: true, sub: "user-id-123", email: "test@test.com");
        var nextCalled = false;
        var middleware = new KeycloakAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.True(nextCalled);
        Assert.Equal("user-id-123", context.Request.Headers["X-User-Id"].ToString());
        Assert.Equal("test@test.com", context.Request.Headers["X-User-Email"].ToString());
    }

    [Fact]
    public async Task PaymentPath_SkipsJwtCheck_CallsNext()
    {
        var context = BuildContext("/payments/auth", isAuthenticated: false);
        var nextCalled = false;
        var middleware = new KeycloakAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
```

- [ ] **Step 2: Test çalıştır — fail bekleniyor**

```bash
dotnet test Gateway.Api.Tests/Gateway.Api.Tests.csproj --filter "KeycloakAuthMiddlewareTests"
```

Expected: FAIL — KeycloakAuthMiddleware does not exist

- [ ] **Step 3: KeycloakAuthMiddleware implementasyonunu yaz**

`Gateway.Api/Middleware/KeycloakAuthMiddleware.cs`:

```csharp
namespace Gateway.Api.Middleware;

public sealed class KeycloakAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/payments"))
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var sub   = context.User.FindFirst("sub")?.Value ?? string.Empty;
            var email = context.User.FindFirst("email")?.Value ?? string.Empty;
            var roles = string.Join(",", context.User.FindAll("roles").Select(c => c.Value));

            context.Request.Headers["X-User-Id"]    = sub;
            context.Request.Headers["X-User-Email"] = email;
            context.Request.Headers["X-User-Roles"] = roles;
        }

        await next(context);
    }
}
```

- [ ] **Step 4: Test çalıştır — pass bekleniyor**

```bash
dotnet test Gateway.Api.Tests/Gateway.Api.Tests.csproj --filter "KeycloakAuthMiddlewareTests"
```

Expected: 3 passed, 0 failed

- [ ] **Step 5: Commit**

```bash
git add Gateway.Api/Middleware/KeycloakAuthMiddleware.cs Gateway.Api.Tests/Middleware/KeycloakAuthMiddlewareTests.cs
git commit -m "feat: add KeycloakAuthMiddleware with tests"
```

---

## Task 9: ApiKeyMiddleware (TDD)

**Files:**
- Create: `Gateway.Api/Middleware/ApiKeyMiddleware.cs`
- Create: `Gateway.Api.Tests/Middleware/ApiKeyMiddlewareTests.cs`

API key hash'leme için SHA256 one-liner (ApiKeyValue.HashKey ile aynı):
`Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))`

- [ ] **Step 1: Failing testleri yaz**

`Gateway.Api.Tests/Middleware/ApiKeyMiddlewareTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Gateway.Api.Cache;
using Gateway.Api.Middleware;
using Gateway.Api.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using PaymentGateway.SharedContracts;
using Xunit;

namespace Gateway.Api.Tests.Middleware;

public class ApiKeyMiddlewareTests
{
    private static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static HttpContext BuildContext(string path, string? apiKey = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (apiKey is not null)
            context.Request.Headers["X-Api-Key"] = apiKey;
        return context;
    }

    [Fact]
    public async Task NonPaymentPath_SkipsApiKeyCheck_CallsNext()
    {
        var cache = new Mock<IApiKeyCacheService>();
        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, cache.Object);
        var context = BuildContext("/merchants/123");

        await middleware.InvokeAsync(context, null!);

        Assert.Equal(200, context.Response.StatusCode);
        cache.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaymentPath_MissingApiKey_Returns401()
    {
        var cache = new Mock<IApiKeyCacheService>();
        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, cache.Object);
        var context = BuildContext("/payments/auth");

        await middleware.InvokeAsync(context, null!);

        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task PaymentPath_ValidKeyFromCache_InjectsHeadersAndCallsNext()
    {
        var rawKey = "pfk_testkey123";
        var hash = Hash(rawKey);
        var merchantId = Guid.NewGuid();
        var entry = new MerchantCacheEntry(merchantId, "TestMerchant", MerchantStatus.Active, ApiKeyStatus.Active);

        var cache = new Mock<IApiKeyCacheService>();
        cache.Setup(x => x.GetAsync(hash, It.IsAny<CancellationToken>()))
             .ReturnsAsync(entry);

        var nextCalled = false;
        var middleware = new ApiKeyMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, cache.Object);
        var context = BuildContext("/payments/auth", rawKey);

        await middleware.InvokeAsync(context, null!);

        Assert.True(nextCalled);
        Assert.Equal(merchantId.ToString(), context.Request.Headers["X-Merchant-Id"].ToString());
        Assert.Equal("TestMerchant", context.Request.Headers["X-Merchant-Name"].ToString());
    }

    [Fact]
    public async Task PaymentPath_RevokedKey_Returns401()
    {
        var rawKey = "pfk_revokedkey";
        var hash = Hash(rawKey);
        var entry = new MerchantCacheEntry(Guid.NewGuid(), "TestMerchant", MerchantStatus.Active, ApiKeyStatus.Revoked);

        var cache = new Mock<IApiKeyCacheService>();
        cache.Setup(x => x.GetAsync(hash, It.IsAny<CancellationToken>()))
             .ReturnsAsync(entry);

        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, cache.Object);
        var context = BuildContext("/payments/auth", rawKey);

        await middleware.InvokeAsync(context, null!);

        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task PaymentPath_SuspendedMerchant_Returns403()
    {
        var rawKey = "pfk_suspendedkey";
        var hash = Hash(rawKey);
        var entry = new MerchantCacheEntry(Guid.NewGuid(), "TestMerchant", MerchantStatus.Suspended, ApiKeyStatus.Active);

        var cache = new Mock<IApiKeyCacheService>();
        cache.Setup(x => x.GetAsync(hash, It.IsAny<CancellationToken>()))
             .ReturnsAsync(entry);

        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, cache.Object);
        var context = BuildContext("/payments/auth", rawKey);

        await middleware.InvokeAsync(context, null!);

        Assert.Equal(403, context.Response.StatusCode);
    }
}
```

- [ ] **Step 2: Test çalıştır — fail bekleniyor**

```bash
dotnet test Gateway.Api.Tests/Gateway.Api.Tests.csproj --filter "ApiKeyMiddlewareTests"
```

Expected: FAIL — ApiKeyMiddleware does not exist

- [ ] **Step 3: ApiKeyMiddleware implementasyonunu yaz**

`Gateway.Api/Middleware/ApiKeyMiddleware.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Gateway.Api.Cache;
using PaymentGateway.SyncContracts.Merchant;

namespace Gateway.Api.Middleware;

public sealed class ApiKeyMiddleware(RequestDelegate next, IApiKeyCacheService cache)
{
    private static string HashKey(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public async Task InvokeAsync(
        HttpContext context,
        SyncMerchantService.SyncMerchantServiceClient grpcClient)
    {
        if (!context.Request.Path.StartsWithSegments("/payments"))
        {
            await next(context);
            return;
        }

        var rawKey = context.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(rawKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var hash  = HashKey(rawKey);
        var entry = await cache.GetAsync(hash, context.RequestAborted);

        if (entry is null)
        {
            var grpcResponse = await grpcClient.GetMerchantByApiKeyAsync(
                new ApiKeyRequest { KeyHash = hash },
                cancellationToken: context.RequestAborted);

            if (!grpcResponse.Found)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            entry = new Models.MerchantCacheEntry(
                MerchantId: Guid.Parse(grpcResponse.MerchantId),
                MerchantName: grpcResponse.MerchantName,
                MerchantStatus: (MerchantStatus)grpcResponse.MerchantStatus,
                ApiKeyStatus: (ApiKeyStatus)grpcResponse.KeyStatus);

            if (entry.MerchantStatus == MerchantStatus.Active && entry.ApiKeyStatus == ApiKeyStatus.Active)
                await cache.SetAsync(hash, entry, context.RequestAborted);
        }

        if (entry.ApiKeyStatus != ApiKeyStatus.Active)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (entry.MerchantStatus != MerchantStatus.Active)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Request.Headers["X-Merchant-Id"]   = entry.MerchantId.ToString();
        context.Request.Headers["X-Merchant-Name"] = entry.MerchantName;

        await next(context);
    }
}
```

Not: `InvokeAsync` iki parametreyi middleware DI'dan alır (`cache` constructor'da, `grpcClient` DI container'dan). Test sınıfında `grpcClient` mock'lanmadığı için constructor-only DI kullanarak testi basit tutmak için `IApiKeyCacheService`'i constructor'a, `grpcClient`'ı method injection'a bırakıyoruz. Test'te gRPC olmayan yolları test ediyoruz; gRPC path için integration test yazılabilir.

- [ ] **Step 4: Test çalıştır — pass bekleniyor**

```bash
dotnet test Gateway.Api.Tests/Gateway.Api.Tests.csproj --filter "ApiKeyMiddlewareTests"
```

Expected: 5 passed, 0 failed

- [ ] **Step 5: Commit**

```bash
git add Gateway.Api/Middleware/ApiKeyMiddleware.cs Gateway.Api.Tests/Middleware/ApiKeyMiddlewareTests.cs
git commit -m "feat: add ApiKeyMiddleware with tests"
```

---

## Task 10: Gateway — Event consumers

**Files:**
- Create: `Gateway.Api/EventHandlers/ApiKeyRevokedHandler.cs`
- Create: `Gateway.Api/EventHandlers/MerchantStatusChangedHandler.cs`

- [ ] **Step 1: ApiKeyRevokedHandler oluştur**

`Gateway.Api/EventHandlers/ApiKeyRevokedHandler.cs`:

```csharp
using Gateway.Api.Cache;

namespace Gateway.Api.EventHandlers;

public static class ApiKeyRevokedHandler
{
    public static async Task Handle(
        ApiKeyRevoked evt,
        IApiKeyCacheService cache,
        CancellationToken ct)
    {
        await cache.RemoveAsync(evt.ApiKeyHash, ct);
    }
}
```

- [ ] **Step 2: MerchantStatusChangedHandler oluştur**

`Gateway.Api/EventHandlers/MerchantStatusChangedHandler.cs`:

```csharp
using Gateway.Api.Cache;

namespace Gateway.Api.EventHandlers;

public static class MerchantStatusChangedHandler
{
    public static async Task Handle(
        MerchantStatusChanged evt,
        IApiKeyCacheService cache,
        CancellationToken ct)
    {
        foreach (var hash in evt.ApiKeyHashes)
            await cache.RemoveAsync(hash, ct);
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Gateway.Api/Gateway.Api.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 4: Commit**

```bash
git add Gateway.Api/EventHandlers/
git commit -m "feat: add ApiKeyRevokedHandler and MerchantStatusChangedHandler"
```

---

## Task 11: Gateway — DependencyExtensions ve Program.cs

**Files:**
- Create: `Gateway.Api/Dependencies/DependencyExtensions.cs`
- Create: `Gateway.Api/Program.cs`

- [ ] **Step 1: DependencyExtensions oluştur**

`Gateway.Api/Dependencies/DependencyExtensions.cs`:

```csharp
using Common.Caching;
using Common.Caching.Redis;
using Common.Dependencies.Models;

namespace Gateway.Api.Dependencies;

public static class DependencyExtensions
{
    public static void AddAllDependencies(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromApplicationDependencies()
            .AddClasses(c => c.AssignableTo<ITransientDependency>()).AsImplementedInterfaces().WithTransientLifetime()
            .AddClasses(c => c.AssignableTo<IScopedDependency>()).AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo<ISingletonDependency>()).AsImplementedInterfaces().WithSingletonLifetime()
        );
    }
}
```

Not: `Scrutor` paketi `Common` üzerinden transitif gelir.

- [ ] **Step 2: Program.cs oluştur**

`Gateway.Api/Program.cs`:

```csharp
using System.Reflection;
using Gateway.Api.Dependencies;
using Gateway.Api.Middleware;
using PaymentGateway.SyncContracts.Merchant;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddKeycloakJwtAuthentication();

builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCachingServices();
var redisConn = builder.Configuration.GetConnectionString("redis");
if (!string.IsNullOrEmpty(redisConn))
    builder.Services.AddRedisCache(redisConn);

builder.Services
    .AddGrpcClient<SyncMerchantService.SyncMerchantServiceClient>(
        o => o.Address = new Uri("https+http://merchant-management"))
    .AddServiceDiscovery();

var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq")!;
builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

    var transport = opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    transport.BindExchange(ExchangeConstants.ApiKeyRevoked, ExchangeType.Fanout)
        .ToQueue("gateway.merchant-events");
    transport.BindExchange(ExchangeConstants.MerchantStatusChanged, ExchangeType.Fanout)
        .ToQueue("gateway.merchant-events");

    opts.ListenToRabbitQueue("gateway.merchant-events");
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseMiddleware<KeycloakAuthMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapReverseProxy();
app.Run();
```

- [ ] **Step 3: Build**

```bash
dotnet build Gateway.Api/Gateway.Api.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 4: Tüm testleri çalıştır**

```bash
dotnet test Gateway.Api.Tests/Gateway.Api.Tests.csproj
```

Expected: Tüm testler geçiyor

- [ ] **Step 5: Commit**

```bash
git add Gateway.Api/Dependencies/ Gateway.Api/Program.cs
git commit -m "feat: complete Gateway.Api with YARP, auth middleware, and event consumers"
```

---

## Task 12: ServiceDefaults — AddGatewayIdentity()

**Files:**
- Modify: `ServiceDefaults/Extensions.cs`

- [ ] **Step 1: AddGatewayIdentity() extension metodunu ekle**

`ServiceDefaults/Extensions.cs` içinde `AddKeycloakJwtAuthentication` metodundan sonra yeni metod ekle:

```csharp
public static TBuilder AddGatewayIdentity<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddTransient<ICurrentUser>(provider =>
    {
        var ctx = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var userId = ctx?.Request.Headers["X-User-Id"].ToString();

        if (string.IsNullOrEmpty(userId))
            return new CurrentUser();

        return new CurrentUser
        {
            Id    = Guid.TryParse(userId, out var id) ? id : Guid.Empty,
            Email = ctx?.Request.Headers["X-User-Email"].ToString()
        };
    });

    return builder;
}
```

`ICurrentUser`, `CurrentUser` için gerekli using:
```csharp
using Common.Auths;
```

- [ ] **Step 2: Build**

```bash
dotnet build ServiceDefaults/ServiceDefaults.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add ServiceDefaults/Extensions.cs
git commit -m "feat: add AddGatewayIdentity to ServiceDefaults"
```

---

## Task 13: Downstream servisler — AddGatewayIdentity'e geçiş

Her servis için aynı pattern uygulanır. **IAM, MerchantManagement, BankIntegration, CommissionManagement, Settlement, PaymentProcessing** — 6 servis.

**Files (her servis için):**
- Modify: `{Service}/Program.cs`
- Modify: `{Service}/Auths/AuthExtensions.cs` (PaymentProcessing hariç)
- Modify: `PaymentProcessing.Api/Domains/PaymentTransactions/Middleware/MerchantMiddleware.cs`

- [ ] **Step 1: Her servisin Program.cs'inde `AddKeycloakJwtAuthentication()` satırını `AddGatewayIdentity()` ile değiştir**

Değiştirilecek servisler ve dosyaları:
- `IAM.Api/Program.cs`
- `MerchantManagement.Api/Program.cs`
- `BankIntegration.Api/Program.cs`
- `CommissionManagement.Api/Program.cs`
- `Settlement.Api/Program.cs`
- `PaymentProcessing.Api/Program.cs`

Her dosyada:
```csharp
// Kaldır:
builder.AddKeycloakJwtAuthentication();

// Ekle:
builder.AddGatewayIdentity();
```

- [ ] **Step 2: Her servisin Program.cs'inde `app.UseAuthentication()` ve `app.UseAuthorization()` satırlarını kaldır**

Bu middleware'ler artık gereksiz — JWT scheme kayıtlı değil.

- [ ] **Step 3: Her servisin AuthExtensions.cs'ini header okuyacak şekilde güncelle**

`IAM.Api/Auths/AuthExtensions.cs`, `MerchantManagement.Api/Auths/AuthExtensions.cs`, `BankIntegration.Api/Auths/AuthExtensions.cs`, `CommissionManagement.Api/Auths/AuthExtensions.cs`, `Settlement.Api/Auths/AuthExtensions.cs` — hepsinde `LoadCurrentUser` metodu header'dan okuyacak şekilde güncelle:

```csharp
public static void LoadCurrentUser(this IServiceCollection serviceCollection)
{
    serviceCollection.AddTransient<ICurrentUser>(provider =>
    {
        var ctx = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var userId = ctx?.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrEmpty(userId)) return new CurrentUser();
        return new CurrentUser
        {
            Id    = Guid.TryParse(userId, out var id) ? id : Guid.Empty,
            Email = ctx?.Request.Headers["X-User-Email"].ToString()
        };
    });
}
```

Not: `AddGatewayIdentity()` ServiceDefaults'ta zaten bu kaydı yapıyor. Eğer servis hem `AddGatewayIdentity()` hem `LoadCurrentUser()` çağırıyorsa, son kayıt kazanır (override). İki çağrı tutarlı olduğu için sorun yok; isterseniz `LoadCurrentUser()` çağrısını Program.cs'den kaldırabilirsiniz.

- [ ] **Step 4: MerchantMiddleware'i header'dan okuyacak şekilde güncelle**

`PaymentProcessing.Api/Domains/PaymentTransactions/Middleware/MerchantMiddleware.cs`:

```csharp
namespace PaymentProcessing.Api.Domains.PaymentTransactions.Middleware;

public class MerchantMiddleware
{
    public static (HandlerContinuation, MerchantIdentity) Before(
        IHttpContextAccessor httpContextAccessor)
    {
        var headers = httpContextAccessor.HttpContext?.Request.Headers;
        var merchantId   = headers?["X-Merchant-Id"].ToString();
        var merchantName = headers?["X-Merchant-Name"].ToString();

        if (string.IsNullOrEmpty(merchantId))
            throw new UnauthorizedAccessException("Merchant context missing.");

        return (HandlerContinuation.Continue, new MerchantIdentity(
            Guid.Parse(merchantId),
            merchantName ?? string.Empty));
    }
}
```

- [ ] **Step 5: Tüm servisleri build et**

```bash
dotnet build PaymentGateway.slnx
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 6: Commit**

```bash
git add IAM.Api/ MerchantManagement.Api/Auths/ MerchantManagement.Api/Program.cs \
        BankIntegration.Api/ CommissionManagement.Api/ Settlement.Api/ \
        PaymentProcessing.Api/Program.cs \
        PaymentProcessing.Api/Domains/PaymentTransactions/Middleware/MerchantMiddleware.cs
git commit -m "refactor: switch all downstream services from Keycloak JWT to gateway identity headers"
```

---

## Task 14: AppHost — Gateway.Api wiring

**Files:**
- Modify: `AppHost/AppHost.cs`

- [ ] **Step 1: AppHost.cs'e gateway ekle ve downstream servisleri güncelle**

`builder.Build().Run();` satırından önce gateway tanımını ekle:

```csharp
var gateway = builder.AddProject<Projects.Gateway_Api>("gateway")
    .WithReference(redis)
    .WithReference(rabbitmq)
    .WithReference(keycloak)
    .WithReference(iamApi)
    .WithReference(merchantApi)
    .WithReference(bankIntApi)
    .WithReference(commissionApi)
    .WithReference(settlementApi)
    .WithReference(paymentApi)
    .WithEnvironment("Keycloak__Authority", "http://localhost:8080/realms/payment-gateway")
    .WaitFor(redis).WaitFor(rabbitmq).WaitFor(keycloak)
    .WaitFor(iamApi).WaitFor(merchantApi).WaitFor(bankIntApi)
    .WaitFor(commissionApi).WaitFor(settlementApi).WaitFor(paymentApi);
```

Downstream servislerin `WithEnvironment("Keycloak__Authority", ...)` satırlarını kaldır — artık bu env var downstream'e gerekmez, sadece gateway'de.

- [ ] **Step 2: AppHost csproj'una Gateway.Api referansı ekle**

`AppHost/AppHost.csproj` içindeki `<ItemGroup>` bloğuna:

```xml
<ProjectReference Include="..\Gateway.Api\Gateway.Api.csproj" />
```

- [ ] **Step 3: Build**

```bash
dotnet build AppHost/AppHost.csproj
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 4: Tüm solution build**

```bash
dotnet build PaymentGateway.slnx
```

Expected: Build succeeded, 0 Error(s)

- [ ] **Step 5: Tüm testleri çalıştır**

```bash
dotnet test Gateway.Api.Tests/Gateway.Api.Tests.csproj
```

Expected: Tüm testler geçiyor

- [ ] **Step 6: Commit**

```bash
git add AppHost/
git commit -m "feat: wire Gateway.Api into Aspire AppHost"
```

---

## Özet

| Task | Ne yapıldı |
|---|---|
| 1 | SharedContracts'a `ApiKeyRevoked` + `MerchantStatusChanged` eklendi |
| 2 | Proto'ya `GetMerchantByApiKey` eklendi |
| 3 | MerchantManagement gRPC impl |
| 4 | MerchantManagement event publish + RabbitMQ wiring |
| 5 | Gateway.Api projesi oluşturuldu |
| 6 | Test projesi oluşturuldu |
| 7 | `ApiKeyCacheService` + testler |
| 8 | `KeycloakAuthMiddleware` + testler |
| 9 | `ApiKeyMiddleware` + testler |
| 10 | Event consumers |
| 11 | `Program.cs` + `DependencyExtensions` |
| 12 | `AddGatewayIdentity()` ServiceDefaults'a eklendi |
| 13 | Tüm downstream servisler gateway'e geçti |
| 14 | AppHost wiring |