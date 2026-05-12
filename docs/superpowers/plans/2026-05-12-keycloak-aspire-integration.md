# Keycloak + Aspire Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keycloak'ı Aspire AppHost'a eklemek, IAM.Api'den custom JWT üretimini kaldırarak Keycloak'a devretmek, ve tüm servislerin Keycloak JWT doğrulamasına geçişini sağlamak.

**Architecture:** Keycloak kimlik doğrulamayı (login, token üretimi, şifre yönetimi) devralır. IAM.Api UserManagement'a dönüşür — User profili Marten'da, `User.Id` = Keycloak `sub` UUID. Diğer servisler her istekte Keycloak JWT imzasını JWKS ile lokalde doğrular.

**Tech Stack:** .NET 10, Aspire 9.3.x, Keycloak 26.x, Aspire.Hosting.Keycloak, Microsoft.AspNetCore.Authentication.JwtBearer (AspNetCore.App framework ref), Wolverine, Marten

---

## Dosya Haritası

**Oluşturulacak:**
- `AppHost/keycloak/realms/payment-gateway.json`
- `IAM.Api/Keycloak/KeycloakTokenProvider.cs`
- `IAM.Api/Keycloak/KeycloakAdminClient.cs`

**Değiştirilecek:**
- `AppHost/AppHost.csproj` — `Aspire.Hosting.Keycloak` paketi
- `AppHost/AppHost.cs` — Keycloak resource + servis env var'ları
- `ServiceDefaults/Extensions.cs` — `AddKeycloakJwtAuthentication()` extension
- `Common/Auths/ICurrentUser.cs` — `ISingletonDependency` → `ITransientDependency`
- `Common/Auths/CurrentUser.cs` — `Load(string token)` → `Load(ClaimsPrincipal principal)`
- `IAM.Api/Domains/Users/User.cs` — `Password` field, `Login()`, `ChangePassword()` kaldırılır; `User.Create` imzası güncellenir
- `IAM.Api/Domains/Users/Features/Commands/CreateUser.cs` — Keycloak Admin API çağrısı
- `IAM.Api/Domains/Users/Features/Commands/ChangePassword.cs` — Keycloak Admin API çağrısı
- `IAM.Api/Auths/AuthExtensions.cs` — `ClaimsPrincipal`'dan okuma
- `IAM.Api/Program.cs` — JWT helper kaldırılır, Keycloak auth eklenir
- `IAM.Api/appsettings.json` — Keycloak config
- `IAM.Api/GlobalUsings.cs` — `System.Security.Cryptography`, `System.Text`, `Common.Utils.Helpers` kaldırılır
- Her servis `Program.cs` — `AddKeycloakJwtAuthentication()` + pipeline middleware

**Silinecek:**
- `IAM.Api/Domains/Users/Features/Commands/Login.cs`
- `IAM.Api/Domains/Users/Features/Endpoints/AuthEndpoints.cs`
- `IAM.Api/Domains/Users/ValueObjects/PasswordHash.cs`
- `Common/Utils/Helpers/IJwtHelper.cs`

---

## Task 1: Realm JSON

**Files:**
- Create: `AppHost/keycloak/realms/payment-gateway.json`

- [ ] **Step 1: Realm JSON'ı oluştur**

`AppHost/keycloak/realms/` dizini oluştur ve dosyayı yaz:

```json
{
  "realm": "payment-gateway",
  "enabled": true,
  "accessTokenLifespan": 3600,
  "clients": [
    {
      "clientId": "payment-api",
      "enabled": true,
      "publicClient": false,
      "directAccessGrantsEnabled": true,
      "serviceAccountsEnabled": true,
      "secret": "payment-api-secret",
      "defaultClientScopes": ["openid", "email", "profile"]
    }
  ],
  "roles": {
    "realm": [
      { "name": "admin" },
      { "name": "merchant_user" }
    ]
  },
  "users": [
    {
      "username": "service-account-payment-api",
      "enabled": true,
      "serviceAccountClientId": "payment-api",
      "clientRoles": {
        "realm-management": ["manage-users"]
      }
    }
  ]
}
```

- [ ] **Step 2: Commit**

```bash
git add AppHost/keycloak/realms/payment-gateway.json
git commit -m "feat: add Keycloak payment-gateway realm config"
```

---

## Task 2: AppHost — Keycloak Resource

**Files:**
- Modify: `AppHost/AppHost.csproj`
- Modify: `AppHost/AppHost.cs`

- [ ] **Step 1: AppHost.csproj'a paket ekle**

`AppHost/AppHost.csproj` içindeki `<ItemGroup>` paket listesine ekle:

```xml
<PackageReference Include="Aspire.Hosting.Keycloak" Version="9.3.0" />
```

- [ ] **Step 2: dotnet restore**

```bash
dotnet restore AppHost/AppHost.csproj
```

Beklenen: paket indirilir, hata yok.

- [ ] **Step 3: AppHost.cs'e Keycloak resource ekle**

`AppHost/AppHost.cs` dosyasını şu şekilde güncelle:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var keycloak = builder.AddKeycloak("keycloak", port: 8080)
    .WithRealmImport("./keycloak/realms")
    .WithLifetime(ContainerLifetime.Persistent);

var iamDb = postgres.AddDatabase("iamDb");
var merchantDb = postgres.AddDatabase("merchantDb");
var paymentDb = postgres.AddDatabase("paymentDb");
var bankIntDb = postgres.AddDatabase("bankIntegrationDb");
var commissionDb = postgres.AddDatabase("commissionDb");
var settlementDb = postgres.AddDatabase("settlementDb");

var garanti = builder.AddProject<Projects.GarantiService>("garanti");

var merchantApi = builder.AddProject<Projects.MerchantManagement_Api>("merchant-management")
    .WithReference(rabbitmq).WithReference(merchantDb)
    .WithEnvironment("Keycloak__Authority", "http://localhost:8080/realms/payment-gateway")
    .WaitFor(rabbitmq).WaitFor(merchantDb);

var bankIntApi = builder.AddProject<Projects.BankIntegration_Api>("bank-integration")
    .WithReference(rabbitmq).WithReference(bankIntDb)
    .WithEnvironment("Keycloak__Authority", "http://localhost:8080/realms/payment-gateway")
    .WaitFor(rabbitmq).WaitFor(bankIntDb);

var commissionApi = builder.AddProject<Projects.CommissionManagement_Api>("commission-management")
    .WithReference(rabbitmq).WithReference(commissionDb)
    .WithEnvironment("Keycloak__Authority", "http://localhost:8080/realms/payment-gateway")
    .WaitFor(rabbitmq).WaitFor(commissionDb);

var paymentApi = builder.AddProject<Projects.PaymentProcessing_Api>("payment-processing")
    .WithReference(rabbitmq).WithReference(paymentDb).WithReference(garanti)
    .WithReference(bankIntApi).WithReference(commissionApi)
    .WithEnvironment("Keycloak__Authority", "http://localhost:8080/realms/payment-gateway")
    .WaitFor(rabbitmq).WaitFor(paymentDb).WaitFor(garanti).WaitFor(bankIntApi).WaitFor(commissionApi);

var iamApi = builder.AddProject<Projects.IAM_Api>("iam")
    .WithReference(keycloak)
    .WithReference(rabbitmq).WithReference(iamDb).WithReference(redis)
    .WaitFor(keycloak).WaitFor(rabbitmq).WaitFor(iamDb).WaitFor(redis);

var settlementApi = builder.AddProject<Projects.Settlement_Api>("settlement")
    .WithReference(rabbitmq).WithReference(settlementDb)
    .WithEnvironment("Keycloak__Authority", "http://localhost:8080/realms/payment-gateway")
    .WaitFor(rabbitmq).WaitFor(settlementDb);

builder.Build().Run();
```

- [ ] **Step 4: Build kontrolü**

```bash
dotnet build AppHost/AppHost.csproj
```

Beklenen: Build succeeded, 0 error(s).

- [ ] **Step 5: Commit**

```bash
git add AppHost/AppHost.csproj AppHost/AppHost.cs
git commit -m "feat: add Keycloak to Aspire AppHost"
```

---

## Task 3: ServiceDefaults — JWT Extension

**Files:**
- Modify: `ServiceDefaults/Extensions.cs`

- [ ] **Step 1: `AddKeycloakJwtAuthentication` extension'ı ekle**

`ServiceDefaults/Extensions.cs` dosyasında `MapDefaultEndpoints` metodunun hemen üstüne şu extension'ı ekle:

```csharp
public static TBuilder AddKeycloakJwtAuthentication<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Keycloak:Authority"];
            options.Audience = "payment-api";
            options.RequireHttpsMetadata = false;
        });

    builder.Services.AddAuthorization();
    return builder;
}
```

Dosyanın üstüne using ekle (eğer yoksa — `FrameworkReference` içinden gelir, explicit using gerekmeyebilir):
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
```

- [ ] **Step 2: Build kontrolü**

```bash
dotnet build ServiceDefaults/ServiceDefaults.csproj
```

Beklenen: Build succeeded, 0 error(s).

- [ ] **Step 3: Commit**

```bash
git add ServiceDefaults/Extensions.cs
git commit -m "feat: add AddKeycloakJwtAuthentication to ServiceDefaults"
```

---

## Task 4: Common — ICurrentUser ve CurrentUser Güncelleme

**Files:**
- Modify: `Common/Auths/ICurrentUser.cs`
- Modify: `Common/Auths/CurrentUser.cs`

- [ ] **Step 1: `ICurrentUser` lifetime'ını düzelt**

`Common/Auths/ICurrentUser.cs` dosyasını güncelle:

```csharp
namespace Common.Auths;

public interface ICurrentUser : ITransientDependency
{
    public Guid Id { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
```

`ISingletonDependency`'den `ITransientDependency`'e geçiş: `CurrentUser` HTTP context'ten okur, Singleton olması bir önceki request'in verisinin sızdığı bir bug'a yol açıyordu.

- [ ] **Step 2: `CurrentUser.Load` imzasını Keycloak claim'lerine güncelle**

`Common/Auths/CurrentUser.cs` dosyasını güncelle:

```csharp
namespace Common.Auths;

public class CurrentUser : ICurrentUser
{
    public static ICurrentUser Load(ClaimsPrincipal principal) => new CurrentUser
    {
        Id    = Guid.Parse(principal.FindFirstValue("sub")!),
        Email = principal.FindFirstValue("email"),
        Name  = principal.FindFirstValue("given_name") + " "
              + principal.FindFirstValue("family_name"),
    };

    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
```

- [ ] **Step 3: Build kontrolü**

```bash
dotnet build Common/Common.csproj
```

Beklenen: Build succeeded, 0 error(s). `Load(string)` signature değişti; çağıran yerler (IAM.Api `AuthExtensions.cs`) Task 10'da güncellenir.

- [ ] **Step 4: Commit**

```bash
git add Common/Auths/ICurrentUser.cs Common/Auths/CurrentUser.cs
git commit -m "refactor: update CurrentUser to read Keycloak ClaimsPrincipal"
```

---

## Task 5: IAM.Api — User Domain Temizliği

**Files:**
- Modify: `IAM.Api/Domains/Users/User.cs`
- Delete: `IAM.Api/Domains/Users/ValueObjects/PasswordHash.cs`

- [ ] **Step 1: `User.cs`'i güncelle — şifre kaldırılır, `Create` imzası değişir**

`IAM.Api/Domains/Users/User.cs` dosyasını şu şekilde yaz:

```csharp
namespace IAM.Api.Domains.Users;

public sealed class User : AggregateRoot
{
    public Email Email { get; private set; } = null!;
    public FullName FullName { get; private set; } = null!;
    public UserStatus Status { get; private set; }
    public Guid? MerchantId { get; private set; }

    [Newtonsoft.Json.JsonProperty]
    private List<UserRole> _roles = [];
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    private User() { }

    public static ResultDomain<User> Create(
        Guid keycloakId, string email, string firstName, string lastName, Guid? merchantId = null)
    {
        var emailResult = Email.Create(email);
        var nameResult = FullName.Create(firstName, lastName);

        var errors = new List<MessageItem>();
        if (!emailResult.IsSuccess) errors.AddRange(emailResult.Messages!);
        if (!nameResult.IsSuccess) errors.AddRange(nameResult.Messages!);
        if (errors.Count > 0) return ResultDomain<User>.Error(errors);

        return ResultDomain<User>.Ok(new User
        {
            Id = keycloakId,
            Email = emailResult.Data!,
            FullName = nameResult.Data!,
            Status = UserStatus.Active,
            MerchantId = merchantId,
        });
    }

    public void Activate() => Status = UserStatus.Active;

    public ResultDomain Deactivate()
    {
        if (Status == UserStatus.Passive)
            return ResultDomain.Error(new MessageItem { Code = "User.AlreadyPassive" });
        Status = UserStatus.Passive;
        return ResultDomain.Ok();
    }

    public ResultDomain AssignRole(Guid roleId)
    {
        if (_roles.Any(r => r.RoleId == roleId))
            return ResultDomain.Error(new MessageItem { Code = "User.RoleAlreadyAssigned" });
        _roles.Add(UserRole.Create(roleId));
        return ResultDomain.Ok();
    }

    public ResultDomain RemoveRole(Guid roleId)
    {
        var role = _roles.SingleOrDefault(r => r.RoleId == roleId);
        if (role is null)
            return ResultDomain.Error(new MessageItem { Code = "User.RoleNotAssigned" });
        _roles.Remove(role);
        return ResultDomain.Ok();
    }

    public ResultDomain AssignMerchant(Guid merchantId)
    {
        if (MerchantId is not null)
            return ResultDomain.Error(new MessageItem { Code = "User.AlreadyAssignedToMerchant" });
        MerchantId = merchantId;
        return ResultDomain.Ok();
    }

    public ResultDomain RemoveFromMerchant()
    {
        if (MerchantId is null)
            return ResultDomain.Error(new MessageItem { Code = "User.NotAssignedToMerchant" });
        MerchantId = null;
        return ResultDomain.Ok();
    }
}
```

- [ ] **Step 2: `PasswordHash.cs`'i sil**

```bash
rm IAM.Api/Domains/Users/ValueObjects/PasswordHash.cs
```

- [ ] **Step 3: Build kontrolü**

```bash
dotnet build IAM.Api/IAM.Api.csproj
```

Beklenen: Build hatası — `Login.cs` ve `ChangePassword.cs` eski `User.Login()` / `User.ChangePassword()` metodlarını çağırıyor, `PasswordHash` namespace'i yok. Bu hatalar sonraki task'larda giderilir.

- [ ] **Step 4: Commit**

```bash
git add IAM.Api/Domains/Users/User.cs
git rm IAM.Api/Domains/Users/ValueObjects/PasswordHash.cs
git commit -m "refactor: remove password from User aggregate, use Keycloak sub as Id"
```

---

## Task 6: IAM.Api — KeycloakTokenProvider

**Files:**
- Create: `IAM.Api/Keycloak/KeycloakTokenProvider.cs`

- [ ] **Step 1: `IAM.Api/Keycloak/` dizinini oluştur ve `KeycloakTokenProvider.cs` yaz**

```csharp
namespace IAM.Api.Keycloak;

public class KeycloakTokenProvider : ITransientDependency
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public KeycloakTokenProvider(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string> GetAdminTokenAsync(CancellationToken ct = default)
    {
        var baseUrl = _config["Keycloak:AdminApiBaseUrl"]!;
        var realm   = _config["Keycloak:Realm"]!;
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = _config["Keycloak:ClientId"]!,
            ["client_secret"] = _config["Keycloak:ClientSecret"]!,
        };

        var response = await _http.PostAsync(
            $"{baseUrl}/realms/{realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(form), ct);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.GetProperty("access_token").GetString()!;
    }
}
```

- [ ] **Step 2: Build kontrolü**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | grep -E "error|warning" | head -20
```

Beklenen: `KeycloakTokenProvider.cs` derlenir (diğer mevcut hatalar Task 5'ten geliyor, bu task'tan değil).

- [ ] **Step 3: Commit**

```bash
git add IAM.Api/Keycloak/KeycloakTokenProvider.cs
git commit -m "feat: add KeycloakTokenProvider for client credentials flow"
```

---

## Task 7: IAM.Api — KeycloakAdminClient

**Files:**
- Create: `IAM.Api/Keycloak/KeycloakAdminClient.cs`

- [ ] **Step 1: `KeycloakAdminClient.cs` yaz**

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IAM.Api.Keycloak;

public class KeycloakAdminClient : ITransientDependency
{
    private readonly HttpClient _http;
    private readonly KeycloakTokenProvider _tokenProvider;
    private readonly IConfiguration _config;

    public KeycloakAdminClient(
        HttpClient http,
        KeycloakTokenProvider tokenProvider,
        IConfiguration config)
    {
        _http          = http;
        _tokenProvider = tokenProvider;
        _config        = config;
    }

    private string AdminBase =>
        $"{_config["Keycloak:AdminApiBaseUrl"]}/admin/realms/{_config["Keycloak:Realm"]}";

    public async Task<Guid> CreateUserAsync(
        string email, string password, string firstName, string lastName,
        CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAdminTokenAsync(ct);

        var body = new
        {
            username    = email,
            email,
            firstName,
            lastName,
            enabled     = true,
            credentials = new[]
            {
                new { type = "password", value = password, temporary = false }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{AdminBase}/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        // Location header: .../admin/realms/payment-gateway/users/{id}
        var location = response.Headers.Location!.ToString();
        return Guid.Parse(location.Split('/').Last());
    }

    public async Task ResetPasswordAsync(
        Guid keycloakId, string newPassword, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAdminTokenAsync(ct);
        var body  = new { type = "password", value = newPassword, temporary = false };

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{AdminBase}/users/{keycloakId}/reset-password");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteUserAsync(Guid keycloakId, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAdminTokenAsync(ct);

        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{AdminBase}/users/{keycloakId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
```

- [ ] **Step 2: Build kontrolü**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | grep "error CS" | head -10
```

Beklenen: `KeycloakAdminClient.cs` ile ilgili hata yok.

- [ ] **Step 3: Commit**

```bash
git add IAM.Api/Keycloak/KeycloakAdminClient.cs
git commit -m "feat: add KeycloakAdminClient for user management"
```

---

## Task 8: IAM.Api — CreateUser Komutu Güncelleme

**Files:**
- Modify: `IAM.Api/Domains/Users/Features/Commands/CreateUser.cs`

- [ ] **Step 1: `CreateUser.cs`'i güncelle**

```csharp
namespace IAM.Api.Domains.Users.Features.Commands;

public static class CreateUser
{
    public class CreateUserCommand
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
    }

    public class CreateUserResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateUserHandler
    {
        public async Task<FeatureObjectResultModel<CreateUserResponse>> Handle(
            CreateUserCommand cmd,
            IDocumentSession session,
            IAM.Api.Keycloak.KeycloakAdminClient keycloak,
            CancellationToken ct)
        {
            var emailExists = await session.Query<User>()
                .AnyAsync(x => x.Email.Value == cmd.Email, ct);

            if (emailExists)
                return FeatureObjectResultModel<CreateUserResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code  = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            Guid keycloakId;
            try
            {
                keycloakId = await keycloak.CreateUserAsync(
                    cmd.Email, cmd.Password, cmd.FirstName, cmd.LastName, ct);
            }
            catch (Exception)
            {
                return FeatureObjectResultModel<CreateUserResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code  = "User.KeycloakCreateFailed"
                });
            }

            var userResult = User.Create(keycloakId, cmd.Email, cmd.FirstName, cmd.LastName);
            if (!userResult.IsSuccess)
            {
                await keycloak.DeleteUserAsync(keycloakId, ct);
                return FeatureObjectResultModel<CreateUserResponse>.Error(userResult.Messages!);
            }

            session.Store(userResult.Data!);
            return FeatureObjectResultModel<CreateUserResponse>.Ok(
                new CreateUserResponse { Id = userResult.Data!.Id });
        }
    }
}
```

- [ ] **Step 2: Build kontrolü**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | grep "error CS" | head -10
```

Beklenen: `CreateUser.cs` ile ilgili hata yok.

- [ ] **Step 3: Commit**

```bash
git add IAM.Api/Domains/Users/Features/Commands/CreateUser.cs
git commit -m "feat: CreateUser calls Keycloak Admin API before writing UserProfile to Marten"
```

---

## Task 9: IAM.Api — ChangePassword Komutu Güncelleme

**Files:**
- Modify: `IAM.Api/Domains/Users/Features/Commands/ChangePassword.cs`

- [ ] **Step 1: `ChangePassword.cs`'i güncelle**

```csharp
namespace IAM.Api.Domains.Users.Features.Commands;

public static class ChangePassword
{
    public class ChangePasswordCommand
    {
        public required Guid UserId { get; set; }
        public required string NewPassword { get; set; }
    }

    public class ChangePasswordCommandResponse { }

    [Transactional]
    public class ChangePasswordHandler
    {
        public async Task<FeatureObjectResultModel<ChangePasswordCommandResponse>> Handle(
            ChangePasswordCommand cmd,
            IDocumentSession session,
            IAM.Api.Keycloak.KeycloakAdminClient keycloak,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<ChangePasswordCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code  = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            await keycloak.ResetPasswordAsync(cmd.UserId, cmd.NewPassword, ct);
            return FeatureObjectResultModel<ChangePasswordCommandResponse>.Ok(
                new ChangePasswordCommandResponse());
        }
    }
}
```

- [ ] **Step 2: Build kontrolü**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | grep "error CS" | head -10
```

- [ ] **Step 3: Commit**

```bash
git add IAM.Api/Domains/Users/Features/Commands/ChangePassword.cs
git commit -m "feat: ChangePassword delegates to Keycloak Admin API"
```

---

## Task 10: IAM.Api — Login Silme, Program.cs ve GlobalUsings Güncelleme

**Files:**
- Delete: `IAM.Api/Domains/Users/Features/Commands/Login.cs`
- Delete: `IAM.Api/Domains/Users/Features/Endpoints/AuthEndpoints.cs`
- Delete: `Common/Utils/Helpers/IJwtHelper.cs`
- Modify: `IAM.Api/Auths/AuthExtensions.cs`
- Modify: `IAM.Api/Program.cs`
- Modify: `IAM.Api/appsettings.json`
- Modify: `IAM.Api/GlobalUsings.cs`

- [ ] **Step 1: Login.cs, AuthEndpoints.cs, IJwtHelper.cs sil**

```bash
git rm IAM.Api/Domains/Users/Features/Commands/Login.cs
git rm IAM.Api/Domains/Users/Features/Endpoints/AuthEndpoints.cs
git rm Common/Utils/Helpers/IJwtHelper.cs
```

- [ ] **Step 2: `AuthExtensions.cs`'i güncelle — `ClaimsPrincipal`'dan okuma**

`IAM.Api/Auths/AuthExtensions.cs` dosyasını şu şekilde yaz:

```csharp
namespace IAM.Api.Auths;

public static class AuthExtensions
{
    public static void LoadCurrentUser(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ICurrentUser>(provider =>
        {
            var httpContext = provider
                .GetRequiredService<IHttpContextAccessor>().HttpContext;

            if (httpContext?.User?.Identity?.IsAuthenticated != true)
                return new CurrentUser();

            return CurrentUser.Load(httpContext.User);
        });
    }
}
```

- [ ] **Step 3: `Program.cs`'i güncelle**

`IAM.Api/Program.cs` dosyasını şu şekilde yaz:

```csharp
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
```

`LoadCurrentUser()` artık `AddAllDependencies()`'den sonra çağrılıyor; bu, Scrutor'un `CurrentUser`'ı Singleton olarak kaydetmesinin önüne geçer.

- [ ] **Step 4: `appsettings.json` güncelle**

`IAM.Api/appsettings.json` dosyasını şu şekilde yaz:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/payment-gateway",
    "AdminApiBaseUrl": "http://localhost:8080",
    "Realm": "payment-gateway",
    "ClientId": "payment-api",
    "ClientSecret": "payment-api-secret"
  }
}
```

- [ ] **Step 5: `GlobalUsings.cs`'i güncelle — artık kullanılmayan using'ler kaldırılır**

`IAM.Api/GlobalUsings.cs` dosyasını şu şekilde yaz:

```csharp
global using IAM.Api.Utils.Constants;
global using Common.Domains;
global using Common.Dependencies.Models;
global using System.Reflection;
global using System.Text.Json;
global using Common;
global using Common.Caching;
global using Common.Caching.Attributes;
global using Common.Caching.Middleware;
global using Microsoft.OpenApi.Models;
global using Wolverine;
global using Microsoft.AspNetCore.Mvc;
global using Common.Auths;
global using Common.Utils.Constants;
global using Wolverine.Attributes;
global using Wolverine.Marten;
global using Marten;
global using IAM.Api.Domains.Users.Entities;
global using IAM.Api.Domains.Users.Enums;
global using IAM.Api.Domains.Users.ValueObjects;
global using IAM.Api.Domains.Roles;
global using IAM.Api.Domains.Roles.Features.Endpoints;
global using IAM.Api.Domains.Users;
global using Wolverine.RabbitMQ;
global using IAM.Api.Auths;
global using IAM.Api.Domains.Users.Features.Commands;
global using IAM.Api.Domains.Users.Features.Queries;
global using PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;
```

Kaldırılanlar: `System.Security.Cryptography`, `System.Text`, `Common.Utils.Helpers` (hepsi artık sadece silinen `PasswordHash.cs` ve `Login.cs` tarafından kullanılıyordu).

- [ ] **Step 6: IAM.Api tam build kontrolü**

```bash
dotnet build IAM.Api/IAM.Api.csproj
```

Beklenen: Build succeeded, 0 error(s).

- [ ] **Step 7: Commit**

```bash
git add IAM.Api/Auths/AuthExtensions.cs IAM.Api/Program.cs IAM.Api/appsettings.json IAM.Api/GlobalUsings.cs
git commit -m "feat: wire Keycloak auth into IAM.Api, remove custom JWT"
```

---

## Task 11: Diğer Servisler — Keycloak JWT Auth

**Files:**
- Modify: `MerchantManagement.Api/Program.cs:7` — `AddServiceDefaults` satırından hemen sonra
- Modify: `MerchantManagement.Api/appsettings.json`
- Modify: `PaymentProcessing.Api/Program.cs:2` — `AddServiceDefaults` satırından hemen sonra
- Modify: `PaymentProcessing.Api/appsettings.json`
- Modify: `CommissionManagement.Api/Program.cs:5` — `AddServiceDefaults` satırından hemen sonra
- Modify: `CommissionManagement.Api/appsettings.json`
- Modify: `BankIntegration.Api/Program.cs:11` — `AddServiceDefaults` satırından hemen sonra
- Modify: `BankIntegration.Api/appsettings.json`
- Modify: `Settlement.Api/Program.cs:4` — `AddServiceDefaults` satırından hemen sonra
- Modify: `Settlement.Api/appsettings.json`

- [ ] **Step 1: Her servisin `Program.cs`'ine auth ekle**

Her serviste `builder.AddServiceDefaults();` satırının hemen altına şu satırı ekle:

```csharp
builder.AddKeycloakJwtAuthentication();
```

Ve her servisin `app.UseExceptionHandler();` satırının hemen önüne:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

Örnek — `MerchantManagement.Api/Program.cs` sonucu:
```csharp
// builder bölümünde (satır 7'den sonra):
builder.AddServiceDefaults();
builder.AddKeycloakJwtAuthentication();   // ← eklendi

// app pipeline bölümünde (app.UseExceptionHandler()'dan önce):
app.UseAuthentication();    // ← eklendi
app.UseAuthorization();     // ← eklendi
app.UseExceptionHandler();
```

Bunu şu 5 dosyaya uygula:
- `MerchantManagement.Api/Program.cs` (AddServiceDefaults: satır 7, UseExceptionHandler: satır 47)
- `PaymentProcessing.Api/Program.cs` (AddServiceDefaults: satır 2)
- `CommissionManagement.Api/Program.cs` (AddServiceDefaults: satır 5, UseExceptionHandler: satır 44)
- `BankIntegration.Api/Program.cs` (AddServiceDefaults: satır 11, UseExceptionHandler: satır 47)
- `Settlement.Api/Program.cs` (AddServiceDefaults: satır 4, UseExceptionHandler: satır 40)

- [ ] **Step 2: Her servisin `appsettings.json`'ına Keycloak config ekle**

Her servisin `appsettings.json` dosyasında JSON root nesnesine şunu ekle:

```json
"Keycloak": {
  "Authority": "http://localhost:8080/realms/payment-gateway"
}
```

Bu servisler Admin API kullanmıyor; `Authority` token doğrulama için JWKS endpoint'ini türetmek amacıyla yeterli.

- [ ] **Step 3: Tüm solution build kontrolü**

```bash
dotnet build PaymentGateway.sln
```

Beklenen: Build succeeded, 0 error(s).

- [ ] **Step 4: Commit**

```bash
git add MerchantManagement.Api/Program.cs MerchantManagement.Api/appsettings.json \
        PaymentProcessing.Api/Program.cs PaymentProcessing.Api/appsettings.json \
        CommissionManagement.Api/Program.cs CommissionManagement.Api/appsettings.json \
        BankIntegration.Api/Program.cs BankIntegration.Api/appsettings.json \
        Settlement.Api/Program.cs Settlement.Api/appsettings.json
git commit -m "feat: add Keycloak JWT auth to all services"
```

---

## Task 12: Uçtan Uca Doğrulama

- [ ] **Step 1: Aspire ile uygulamayı başlat**

```bash
dotnet run --project AppHost/AppHost.csproj
```

Aspire Dashboard açılır (`http://localhost:15888`). Keycloak, IAM ve diğer servislerin `Running` durumuna geçmesini bekle.

- [ ] **Step 2: Keycloak Admin Console'a giriş**

Tarayıcıda `http://localhost:8080` → admin / admin ile giriş. `payment-gateway` realm'inin oluştuğunu doğrula.

- [ ] **Step 3: Kullanıcı oluştur (IAM API)**

```bash
curl -s -X POST http://localhost:<iam-port>/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test1234!",
    "firstName": "Test",
    "lastName": "User"
  }' | jq .
```

Beklenen: `{ "isSuccess": true, "data": { "id": "<uuid>" } }`

Keycloak Admin Console'da Users bölümünde `test@example.com` kullanıcısının göründüğünü doğrula.

- [ ] **Step 4: Token al (Keycloak)**

```bash
curl -s -X POST http://localhost:8080/realms/payment-gateway/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=test@example.com&password=Test1234!&client_id=payment-api&client_secret=payment-api-secret" \
  | jq .access_token
```

Beklenen: JWT token string döner.

- [ ] **Step 5: Token ile korunan endpoint çağır**

Token'ı `<ACCESS_TOKEN>` olarak kaydet:

```bash
curl -s http://localhost:<iam-port>/api/users \
  -H "Authorization: Bearer <ACCESS_TOKEN>" | jq .
```

Beklenen: Kullanıcı listesi döner (401 değil).

- [ ] **Step 6: Final commit**

```bash
git add .
git commit -m "chore: verify Keycloak integration end-to-end"
```