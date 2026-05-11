# IAM.Api Marten Document Store Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** IAM.Api'deki tüm `IamContext` (EF Core) bağımlılıklarını kaldırıp Marten document store'a geçiş yapmak — User ve Role Marten'da JSON document olarak saklanır.

**Architecture:** `IDocumentSession` Wolverine tarafından handler'lara inject edilir; `session.Store(entity)` ile kayıt, `session.LoadAsync<T>(id)` / `session.Query<T>()` ile okuma yapılır. Value Object'ler (Email, PasswordHash, FullName, RoleName) için custom Newtonsoft JsonConverter yazılır. Private koleksiyon alanları (`_roles`, `_permissions`, `_actions`) `[JsonProperty]` ile işaretlenir.

**Tech Stack:** .NET 10, Marten 8.x (document store), Wolverine 5.x, Newtonsoft.Json 13.x, Redis (session cache)

---

### Task 1: Infrastructure — AppHost, csproj, Program.cs, GlobalUsings

**Files:**
- Modify: `AppHost/AppHost.cs`
- Modify: `IAM.Api/IAM.Api.csproj`
- Modify: `IAM.Api/Program.cs`
- Modify: `IAM.Api/GlobalUsings.cs`

- [ ] **Step 1: AppHost'a Redis referansı ekle**

`AppHost/AppHost.cs` içinde `iamApi` tanımını güncelle:

```csharp
var iamApi = builder.AddProject<Projects.IAM_Api>("iam")
    .WithReference(rabbitmq).WithReference(iamDb).WithReference(redis)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WaitFor(rabbitmq).WaitFor(iamDb).WaitFor(redis);
```

- [ ] **Step 2: Newtonsoft.Json paketini csproj'a ekle**

`IAM.Api/IAM.Api.csproj` `<ItemGroup>` paket listesine ekle:

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

- [ ] **Step 3: Program.cs'i güncelle — Marten + Wolverine**

`IAM.Api/Program.cs` içindeki Marten ve Wolverine bloklarını aşağıdaki şekilde değiştir:

Mevcut Marten bloğunu şununla değiştir:
```csharp
var iamDb = builder.Configuration.GetConnectionString("defaultDb");
builder.Services.AddMarten(opts =>
{
    opts.Connection(iamDb!);
    opts.UseNewtonsoftForSerialization(s =>
    {
        s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
        s.Converters.Add(new EmailConverter());
        s.Converters.Add(new PasswordHashConverter());
        s.Converters.Add(new FullNameConverter());
        s.Converters.Add(new RoleNameConverter());
    });
    opts.Schema.For<User>().Index(u => u.Email.Value);
    opts.Schema.For<Role>();
})
.IntegrateWithWolverine()
.ApplyAllDatabaseChangesOnStartup();
```

Wolverine bloğundan `opts.UseEntityFrameworkCoreTransactions();` satırını kaldır.

Program.cs başına using ekle:
```csharp
using IAM.Api.Shared.Serialization;
using PaymentGatewayApi.Modules.IAM.Users;
using PaymentGatewayApi.Modules.IAM.Roles;
```

- [ ] **Step 4: GlobalUsings'i güncelle**

`IAM.Api/GlobalUsings.cs` içeriğini şununla değiştir:

```csharp
global using System.Security.Cryptography;
global using System.Text;
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
global using System.Text.Json.Serialization;
global using Wolverine.Attributes;
global using Wolverine.Marten;
global using Marten;
```

- [ ] **Step 5: Build al (Task 2 öncesi kısmi doğrulama — hata beklenir)**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | head -30
```

Beklenen: `EmailConverter` bulunamadı vb. hataları — Task 2'de çözülecek. `IamContext` hataları da görünecek — ilerleyen task'larda çözülecek.

- [ ] **Step 6: Commit**

```bash
git add AppHost/AppHost.cs IAM.Api/IAM.Api.csproj IAM.Api/Program.cs IAM.Api/GlobalUsings.cs
git commit -m "feat(iam): setup Marten document store infrastructure, add Redis reference"
```

---

### Task 2: Serialization Katmanı — JsonConverters + Domain Model Annotations

**Files:**
- Create: `IAM.Api/Shared/Serialization/IamJsonConverters.cs`
- Modify: `IAM.Api/Domains/Users/ValueObjects/FullName.cs`
- Modify: `IAM.Api/Domains/Users/User.cs`
- Modify: `IAM.Api/Domains/Roles/Role.cs`
- Modify: `IAM.Api/Domains/Roles/Entities/PagePermission.cs`

- [ ] **Step 1: FullName'e FromPersistence metodu ekle**

`IAM.Api/Domains/Users/ValueObjects/FullName.cs` içine `Create` metodunun altına ekle:

```csharp
public static FullName FromPersistence(string firstName, string lastName) => new(firstName, lastName);
```

- [ ] **Step 2: IamJsonConverters.cs dosyasını oluştur**

`IAM.Api/Shared/Serialization/IamJsonConverters.cs` dosyasını oluştur:

```csharp
using IAM.Api.Domains.Users.ValueObjects;
using IAM.Api.Domains.Roles.ValueObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IAM.Api.Shared.Serialization;

public class EmailConverter : JsonConverter<Email>
{
    public override Email ReadJson(JsonReader reader, Type objectType, Email? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
        => Email.FromPersistence(reader.Value!.ToString()!);

    public override void WriteJson(JsonWriter writer, Email? value, JsonSerializer serializer)
        => writer.WriteValue(value!.Value);
}

public class PasswordHashConverter : JsonConverter<PasswordHash>
{
    public override PasswordHash ReadJson(JsonReader reader, Type objectType, PasswordHash? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
        => PasswordHash.FromHash(reader.Value!.ToString()!);

    public override void WriteJson(JsonWriter writer, PasswordHash? value, JsonSerializer serializer)
        => writer.WriteValue(value!.Hash);
}

public class FullNameConverter : JsonConverter<FullName>
{
    public override FullName ReadJson(JsonReader reader, Type objectType, FullName? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var firstName = obj["FirstName"]?.Value<string>() ?? "";
        var lastName = obj["LastName"]?.Value<string>() ?? "";
        return FullName.FromPersistence(firstName, lastName);
    }

    public override void WriteJson(JsonWriter writer, FullName? value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("FirstName");
        writer.WriteValue(value!.FirstName);
        writer.WritePropertyName("LastName");
        writer.WriteValue(value!.LastName);
        writer.WriteEndObject();
    }
}

public class RoleNameConverter : JsonConverter<RoleName>
{
    public override RoleName ReadJson(JsonReader reader, Type objectType, RoleName? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
        => RoleName.FromPersistence(reader.Value!.ToString()!);

    public override void WriteJson(JsonWriter writer, RoleName? value, JsonSerializer serializer)
        => writer.WriteValue(value!.Value);
}
```

- [ ] **Step 3: User._roles alanını güncelle**

`IAM.Api/Domains/Users/User.cs` içinde:

```csharp
// DEĞİŞTİR:
private readonly List<UserRole> _roles = [];

// BUNUNLA:
[Newtonsoft.Json.JsonProperty]
private List<UserRole> _roles = [];
```

- [ ] **Step 4: Role._permissions alanını güncelle**

`IAM.Api/Domains/Roles/Role.cs` içinde:

```csharp
// DEĞİŞTİR:
private readonly List<PagePermission> _permissions = [];

// BUNUNLA:
[Newtonsoft.Json.JsonProperty]
private List<PagePermission> _permissions = [];
```

- [ ] **Step 5: PagePermission._actions alanını güncelle**

`IAM.Api/Domains/Roles/Entities/PagePermission.cs` içinde:

```csharp
// DEĞİŞTİR:
private readonly List<PageAction> _actions = [];

// BUNUNLA:
[Newtonsoft.Json.JsonProperty]
private List<PageAction> _actions = [];
```

- [ ] **Step 6: Build al**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | grep -E "error|warning" | head -20
```

Beklenen: `IamContext` hataları kalır, converter hataları gider.

- [ ] **Step 7: Commit**

```bash
git add IAM.Api/Shared/Serialization/IamJsonConverters.cs \
  IAM.Api/Domains/Users/ValueObjects/FullName.cs \
  IAM.Api/Domains/Users/User.cs \
  IAM.Api/Domains/Roles/Role.cs \
  IAM.Api/Domains/Roles/Entities/PagePermission.cs
git commit -m "feat(iam): add Newtonsoft JsonConverters for value objects, annotate private fields"
```

---

### Task 3: User Command Handlers (basit — Redis güncellemesi yok)

Handler pattern'ı: `IamContext db` → `IDocumentSession session`, EF Core sorguları → Marten sorguları, `session.Store(entity)` ile kayıt.

**Files:**
- Modify: `IAM.Api/Domains/Users/Features/Commands/CreateUser.cs`
- Modify: `IAM.Api/Domains/Users/Features/Commands/ActivateUser.cs`
- Modify: `IAM.Api/Domains/Users/Features/Commands/DeactivateUser.cs`
- Modify: `IAM.Api/Domains/Users/Features/Commands/ChangePassword.cs`
- Modify: `IAM.Api/Domains/Users/Features/Commands/AssignUserMerchant.cs`
- Modify: `IAM.Api/Domains/Users/Features/Commands/RemoveUserFromMerchant.cs`

- [ ] **Step 1: CreateUser handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/CreateUser.cs` içindeki `CreateUserHandler.Handle` metodunu:

```csharp
[Transactional]
public class CreateUserHandler
{
    public async Task<FeatureObjectResultModel<CreateUserResponse>> Handle(
        CreateUserCommand cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        var emailExists = await session.Query<User>()
            .AnyAsync(x => x.Email.Value == cmd.Email, ct);
        if (emailExists)
            return FeatureObjectResultModel<CreateUserResponse>.Error(new MessageItem
            {
                Table = nameof(User),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
            });

        var userResult = User.Create(cmd.Email, cmd.Password, cmd.FirstName, cmd.LastName);
        if (!userResult.IsSuccess)
            return FeatureObjectResultModel<CreateUserResponse>.Error(userResult.Messages!);

        session.Store(userResult.Data!);
        return FeatureObjectResultModel<CreateUserResponse>.Ok(new CreateUserResponse
        {
            Id = userResult.Data!.Id
        });
    }
}
```

- [ ] **Step 2: ActivateUser handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/ActivateUser.cs`:

```csharp
[Transactional]
public class ActivateUserHandler
{
    public async Task<FeatureObjectResultModel<ActivateUserCommandResponse>> Handle(
        ActivateUserCommand cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        var user = await session.LoadAsync<User>(cmd.UserId, ct);
        if (user is null)
            return FeatureObjectResultModel<ActivateUserCommandResponse>.Error(new MessageItem
            {
                Table = nameof(User),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });

        user.Activate();
        session.Store(user);
        return FeatureObjectResultModel<ActivateUserCommandResponse>.Ok(
            new ActivateUserCommandResponse { Id = user.Id });
    }
}
```

- [ ] **Step 3: DeactivateUser handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/DeactivateUser.cs`:

```csharp
[Transactional]
public class DeactivateUserHandler
{
    public async Task<FeatureObjectResultModel<DeactivateUserCommandResponse>> Handle(
        DeactivateUserCommand cmd,
        IDocumentSession session,
        ICache cache,
        CancellationToken ct)
    {
        var user = await session.LoadAsync<User>(cmd.UserId, ct);
        if (user is null)
            return FeatureObjectResultModel<DeactivateUserCommandResponse>.Error(new MessageItem
            {
                Table = nameof(User),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });

        var result = user.Deactivate();
        if (!result.IsSuccess)
            return FeatureObjectResultModel<DeactivateUserCommandResponse>.Error(result.Messages!);

        session.Store(user);
        await cache.Remove($"user:{cmd.UserId}");
        return FeatureObjectResultModel<DeactivateUserCommandResponse>.Ok(
            new DeactivateUserCommandResponse { UserId = cmd.UserId });
    }
}
```

- [ ] **Step 4: ChangePassword handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/ChangePassword.cs`:

```csharp
[Transactional]
public class ChangePasswordHandler
{
    public async Task<FeatureObjectResultModel<ChangePasswordCommandResponse>> Handle(
        ChangePasswordCommand cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        var user = await session.LoadAsync<User>(cmd.UserId, ct);
        if (user is null)
            return FeatureObjectResultModel<ChangePasswordCommandResponse>.Error(new MessageItem
            {
                Table = nameof(User),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });

        var result = user.ChangePassword(cmd.NewPassword);
        if (!result.IsSuccess)
            return FeatureObjectResultModel<ChangePasswordCommandResponse>.Error(result.Messages!);

        session.Store(user);
        return FeatureObjectResultModel<ChangePasswordCommandResponse>.Ok(
            new ChangePasswordCommandResponse());
    }
}
```

- [ ] **Step 5: AssignUserMerchant handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/AssignUserMerchant.cs`:

```csharp
[Transactional]
public class AssignUserMerchantHandler
{
    public async Task<FeatureObjectResultModel<AssignUserMerchantCommandResponse>> Handle(
        AssignUserMerchantCommand cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        var user = await session.LoadAsync<User>(cmd.UserId, ct);
        if (user is null)
            return FeatureObjectResultModel<AssignUserMerchantCommandResponse>.Error(new MessageItem
            {
                Table = nameof(User),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });

        var result = user.AssignMerchant(cmd.MerchantId);
        if (!result.IsSuccess)
            return FeatureObjectResultModel<AssignUserMerchantCommandResponse>.Error(result.Messages!);

        session.Store(user);
        return FeatureObjectResultModel<AssignUserMerchantCommandResponse>.Ok(
            new AssignUserMerchantCommandResponse());
    }
}
```

- [ ] **Step 6: RemoveUserFromMerchant handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/RemoveUserFromMerchant.cs`:

```csharp
[Transactional]
public class RemoveUserFromMerchantHandler
{
    public async Task<FeatureObjectResultModel<RemoveUserFromMerchantCommandResponse>> Handle(
        RemoveUserFromMerchantCommand cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        var user = await session.LoadAsync<User>(cmd.UserId, ct);
        if (user is null)
            return FeatureObjectResultModel<RemoveUserFromMerchantCommandResponse>.Error(new MessageItem
            {
                Table = nameof(User),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });

        var result = user.RemoveFromMerchant();
        if (!result.IsSuccess)
            return FeatureObjectResultModel<RemoveUserFromMerchantCommandResponse>.Error(result.Messages!);

        session.Store(user);
        return FeatureObjectResultModel<RemoveUserFromMerchantCommandResponse>.Ok(
            new RemoveUserFromMerchantCommandResponse());
    }
}
```

- [ ] **Step 7: Build al**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | grep "error" | head -20
```

- [ ] **Step 8: Commit**

```bash
git add IAM.Api/Domains/Users/Features/Commands/CreateUser.cs \
  IAM.Api/Domains/Users/Features/Commands/ActivateUser.cs \
  IAM.Api/Domains/Users/Features/Commands/DeactivateUser.cs \
  IAM.Api/Domains/Users/Features/Commands/ChangePassword.cs \
  IAM.Api/Domains/Users/Features/Commands/AssignUserMerchant.cs \
  IAM.Api/Domains/Users/Features/Commands/RemoveUserFromMerchant.cs
git commit -m "feat(iam): migrate simple user command handlers to Marten IDocumentSession"
```

---

### Task 4: User Command Handlers (Redis session güncelleme + Login)

Bu handler'lar rol değişikliği sonrası Redis session'ını yeniden oluşturur. Login ise `Include()` yerine document yükleme yapar.

**Files:**
- Modify: `IAM.Api/Domains/Users/Features/Commands/Login.cs`
- Modify: `IAM.Api/Domains/Users/Features/Commands/AssignUserRole.cs`
- Modify: `IAM.Api/Domains/Users/Features/Commands/RevokeUserRole.cs`
- Modify: `IAM.Api/Domains/Users/Features/Commands/AssignMerchantAdmin.cs`

- [ ] **Step 1: Login handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/Login.cs`:

```csharp
using Common.Utils.Helpers;
using IAM.Api.Auths;
using PaymentGatewayApi.Auths;
using PaymentGatewayApi.Modules.IAM.Roles;
using PaymentGatewayApi.Modules.IAM.Users;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class Login
{
    public class LoginCommand
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    public class LoginCommandResponse
    {
        public string? Token { get; set; }
    }

    [Transactional]
    public class LoginHandler
    {
        public async Task<FeatureObjectResultModel<LoginCommandResponse>> Handle(
            LoginCommand cmd,
            IDocumentSession session,
            IJwtHelper jwtHelper,
            ICache cache,
            CancellationToken ct)
        {
            var user = await session.Query<User>()
                .FirstOrDefaultAsync(x => x.Email.Value == cmd.Email, ct);

            if (user is null || !user.Login(cmd.Password))
                return FeatureObjectResultModel<LoginCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            session.Store(user);

            var roleIds = user.Roles.Select(r => r.RoleId).ToList();
            var roles = await session.Query<Role>()
                .Where(r => roleIds.Contains(r.Id))
                .ToListAsync(ct);

            var pages = roles
                .SelectMany(r => r.Permissions)
                .GroupBy(p => p.PageRoute)
                .Select(g => new PageAccess
                {
                    Route = g.Key,
                    Actions = g.SelectMany(p => p.Actions.Select(a => a.Action)).Distinct().ToList()
                })
                .ToList();

            var claimInfo = new UserClaimInfo(user.FullName.FirstName, user.FullName.LastName, user.Email.Value);
            var accessToken = jwtHelper.Create(claimInfo);

            await cache.Set($"user:{user.Id}", new UserSessionCache
            {
                UserId = user.Id,
                Pages = pages
            });

            return FeatureObjectResultModel<LoginCommandResponse>.Ok(
                new LoginCommandResponse { Token = accessToken.Token });
        }
    }
}
```

- [ ] **Step 2: AssignUserRole handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/AssignUserRole.cs`:

```csharp
using IAM.Api.Auths;
using PaymentGatewayApi.Auths;
using PaymentGatewayApi.Modules.IAM.Roles;
using PaymentGatewayApi.Modules.IAM.Users;

namespace IAM.Api.Domains.Users.Features.Commands;

public static class AssignUserRole
{
    public class AssignUserRoleCommand
    {
        public required Guid UserId { get; set; }
        public required Guid RoleId { get; set; }
    }

    public class AssignUserRoleCommandResponse { }

    [Transactional]
    public class AssignUserRoleHandler
    {
        public async Task<FeatureObjectResultModel<AssignUserRoleCommandResponse>> Handle(
            AssignUserRoleCommand cmd,
            IDocumentSession session,
            ICache cache,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<AssignUserRoleCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = user.AssignRole(cmd.RoleId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AssignUserRoleCommandResponse>.Error(result.Messages!);

            session.Store(user);

            var roleIds = user.Roles.Select(r => r.RoleId).ToList();
            var roles = await session.Query<Role>()
                .Where(r => roleIds.Contains(r.Id))
                .ToListAsync(ct);

            var pages = roles
                .SelectMany(r => r.Permissions)
                .GroupBy(p => p.PageRoute)
                .Select(g => new PageAccess
                {
                    Route = g.Key,
                    Actions = g.SelectMany(p => p.Actions.Select(a => a.Action)).Distinct().ToList()
                })
                .ToList();

            await cache.Set($"user:{cmd.UserId}", new UserSessionCache { UserId = cmd.UserId, Pages = pages });
            return FeatureObjectResultModel<AssignUserRoleCommandResponse>.Ok(
                new AssignUserRoleCommandResponse());
        }
    }
}
```

- [ ] **Step 3: RevokeUserRole handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/RevokeUserRole.cs`:

```csharp
using IAM.Api.Auths;
using PaymentGatewayApi.Auths;
using PaymentGatewayApi.Modules.IAM.Roles;
using PaymentGatewayApi.Modules.IAM.Users;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class RevokeUserRole
{
    public class RevokeUserRoleCommand
    {
        public required Guid UserId { get; set; }
        public required Guid RoleId { get; set; }
    }

    public class RevokeUserRoleCommandResponse { }

    [Transactional]
    public class RevokeUserRoleHandler
    {
        public async Task<FeatureObjectResultModel<RevokeUserRoleCommandResponse>> Handle(
            RevokeUserRoleCommand cmd,
            IDocumentSession session,
            ICache cache,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = user.RemoveRole(cmd.RoleId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Error(result.Messages!);

            session.Store(user);

            var roleIds = user.Roles.Select(r => r.RoleId).ToList();
            var roles = await session.Query<Role>()
                .Where(r => roleIds.Contains(r.Id))
                .ToListAsync(ct);

            var pages = roles
                .SelectMany(r => r.Permissions)
                .GroupBy(p => p.PageRoute)
                .Select(g => new PageAccess
                {
                    Route = g.Key,
                    Actions = g.SelectMany(p => p.Actions.Select(a => a.Action)).Distinct().ToList()
                })
                .ToList();

            await cache.Set($"user:{cmd.UserId}", new UserSessionCache { UserId = cmd.UserId, Pages = pages });
            return FeatureObjectResultModel<RevokeUserRoleCommandResponse>.Ok(
                new RevokeUserRoleCommandResponse());
        }
    }
}
```

- [ ] **Step 4: AssignMerchantAdmin handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Commands/AssignMerchantAdmin.cs`:

```csharp
using PaymentGatewayApi.Modules.IAM.Roles;
using PaymentGatewayApi.Modules.IAM.Users;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class AssignMerchantAdmin
{
    private const string MerchantAdminRoleName = "MerchantAdmin";

    public class AssignMerchantAdminCommand
    {
        public required Guid UserId { get; set; }
        public required Guid MerchantId { get; set; }
    }

    public class AssignMerchantAdminCommandResponse { }

    [Transactional]
    public class AssignMerchantAdminHandler
    {
        public async Task<FeatureObjectResultModel<AssignMerchantAdminCommandResponse>> Handle(
            AssignMerchantAdminCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(cmd.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var merchantAdminRole = await session.Query<Role>()
                .FirstOrDefaultAsync(r => r.Name.Value == MerchantAdminRoleName, ct);
            if (merchantAdminRole is null)
                return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var merchantResult = user.AssignMerchant(cmd.MerchantId);
            if (!merchantResult.IsSuccess)
                return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Error(merchantResult.Messages!);

            var roleResult = user.AssignRole(merchantAdminRole.Id);
            if (!roleResult.IsSuccess)
                return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Error(roleResult.Messages!);

            session.Store(user);
            return FeatureObjectResultModel<AssignMerchantAdminCommandResponse>.Ok(
                new AssignMerchantAdminCommandResponse());
        }
    }
}
```

- [ ] **Step 5: Build al**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | grep "error" | head -20
```

- [ ] **Step 6: Commit**

```bash
git add IAM.Api/Domains/Users/Features/Commands/Login.cs \
  IAM.Api/Domains/Users/Features/Commands/AssignUserRole.cs \
  IAM.Api/Domains/Users/Features/Commands/RevokeUserRole.cs \
  IAM.Api/Domains/Users/Features/Commands/AssignMerchantAdmin.cs
git commit -m "feat(iam): migrate Login, AssignUserRole, RevokeUserRole, AssignMerchantAdmin to Marten"
```

---

### Task 5: User Query Handlers

**Files:**
- Modify: `IAM.Api/Domains/Users/Features/Queries/GetAllUsers.cs`
- Modify: `IAM.Api/Domains/Users/Features/Queries/GetUserById.cs`

- [ ] **Step 1: GetAllUsers handler'ı güncelle**

Marten LINQ projection'u (`Select`) destekler; document yüklendikten sonra client-side map yapılır:

`IAM.Api/Domains/Users/Features/Queries/GetAllUsers.cs`:

```csharp
using IAM.Api.Domains.Users.Enums;
using PaymentGatewayApi.Modules.IAM.Users;

namespace IAM.Api.Domains.Users.Features.Queries;

public static class GetAllUsers
{
    public class GetAllUsersQuery { }

    public class UserListItem
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public UserStatus Status { get; set; }
        public Guid? MerchantId { get; set; }
    }

    public class GetAllUsersHandler
    {
        public async Task<FeatureObjectResultModel<List<UserListItem>>> Handle(
            GetAllUsersQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var users = await session.Query<User>().ToListAsync(ct);
            var result = users.Select(x => new UserListItem
            {
                Id = x.Id,
                Email = x.Email.Value,
                FirstName = x.FullName.FirstName,
                LastName = x.FullName.LastName,
                Status = x.Status,
                MerchantId = x.MerchantId
            }).ToList();

            return FeatureObjectResultModel<List<UserListItem>>.Ok(result);
        }
    }
}
```

- [ ] **Step 2: GetUserById handler'ı güncelle**

`IAM.Api/Domains/Users/Features/Queries/GetUserById.cs`:

```csharp
using IAM.Api.Domains.Users.Enums;
using PaymentGatewayApi.Modules.IAM.Users;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Queries;

public static class GetUserById
{
    public class GetUserByIdQuery
    {
        public required Guid UserId { get; set; }
    }

    public class GetUserByIdResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public UserStatus Status { get; set; }
        public Guid? MerchantId { get; set; }
        public List<Guid> RoleIds { get; set; } = [];
    }

    public class GetUserByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetUserByIdResponse>> Handle(
            GetUserByIdQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var user = await session.LoadAsync<User>(query.UserId, ct);
            if (user is null)
                return FeatureObjectResultModel<GetUserByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetUserByIdResponse>.Ok(new GetUserByIdResponse
            {
                Id = user.Id,
                Email = user.Email.Value,
                FirstName = user.FullName.FirstName,
                LastName = user.FullName.LastName,
                Status = user.Status,
                MerchantId = user.MerchantId,
                RoleIds = user.Roles.Select(r => r.RoleId).ToList()
            });
        }
    }
}
```

- [ ] **Step 3: Build al**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | grep "error" | head -20
```

- [ ] **Step 4: Commit**

```bash
git add IAM.Api/Domains/Users/Features/Queries/GetAllUsers.cs \
  IAM.Api/Domains/Users/Features/Queries/GetUserById.cs
git commit -m "feat(iam): migrate user query handlers to Marten"
```

---

### Task 6: Role Command Handlers

**Files:**
- Modify: `IAM.Api/Domains/Roles/Features/Commands/CreateRole.cs`
- Modify: `IAM.Api/Domains/Roles/Features/Commands/AddRolePermission.cs`
- Modify: `IAM.Api/Domains/Roles/Features/Commands/RemoveRolePermission.cs`

- [ ] **Step 1: CreateRole handler'ı güncelle**

Not: Mevcut dosyada `db` parametresi Handle imzasından eksik — bu adımda düzeltilir.

`IAM.Api/Domains/Roles/Features/Commands/CreateRole.cs`:

```csharp
using PaymentGatewayApi.Modules.IAM.Roles;

namespace IAM.Api.Domains.Roles.Features.Commands;

public static class CreateRole
{
    public class CreateRoleCommand
    {
        public required string Name { get; set; }
        public bool IsSystem { get; set; } = false;
    }

    public class CreateRoleResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateRoleHandler
    {
        public async Task<FeatureObjectResultModel<CreateRoleResponse>> Handle(
            CreateRoleCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var roleResult = Role.Create(cmd.Name, cmd.IsSystem);
            if (!roleResult.IsSuccess)
                return FeatureObjectResultModel<CreateRoleResponse>.Error(roleResult.Messages!);

            session.Store(roleResult.Data!);
            return FeatureObjectResultModel<CreateRoleResponse>.Ok(
                new CreateRoleResponse { Id = roleResult.Data!.Id });
        }
    }
}
```

- [ ] **Step 2: AddRolePermission handler'ı güncelle**

Not: Mevcut dosyada `db` parametresi Handle imzasından eksik — bu adımda düzeltilir.

`IAM.Api/Domains/Roles/Features/Commands/AddRolePermission.cs`:

```csharp
using PaymentGatewayApi.Modules.IAM.Roles;

namespace IAM.Api.Domains.Roles.Features.Commands;

public static class AddRolePermission
{
    public class AddRolePermissionCommand
    {
        public required Guid RoleId { get; set; }
        public required string PageRoute { get; set; }
        public required string Action { get; set; }
    }

    public class AddRolePermissionCommandResponse { }

    [Transactional]
    public class AddRolePermissionHandler
    {
        public async Task<FeatureObjectResultModel<AddRolePermissionCommandResponse>> Handle(
            AddRolePermissionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var role = await session.LoadAsync<Role>(cmd.RoleId, ct);
            if (role is null)
                return FeatureObjectResultModel<AddRolePermissionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = role.AddPermission(cmd.PageRoute, cmd.Action);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddRolePermissionCommandResponse>.Error(result.Messages!);

            session.Store(role);
            return FeatureObjectResultModel<AddRolePermissionCommandResponse>.Ok(
                new AddRolePermissionCommandResponse());
        }
    }
}
```

- [ ] **Step 3: RemoveRolePermission handler'ı güncelle**

`IAM.Api/Domains/Roles/Features/Commands/RemoveRolePermission.cs`:

```csharp
using PaymentGatewayApi.Modules.IAM.Roles;

namespace IAM.Api.Domains.Roles.Features.Commands;

public static class RemoveRolePermission
{
    public class RemoveRolePermissionCommand
    {
        public required Guid RoleId { get; set; }
        public required Guid PermissionId { get; set; }
    }

    public class RemoveRolePermissionCommandResponse { }

    [Transactional]
    public class RemoveRolePermissionHandler
    {
        public async Task<FeatureObjectResultModel<RemoveRolePermissionCommandResponse>> Handle(
            RemoveRolePermissionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var role = await session.LoadAsync<Role>(cmd.RoleId, ct);
            if (role is null)
                return FeatureObjectResultModel<RemoveRolePermissionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = role.RemovePermission(cmd.PermissionId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RemoveRolePermissionCommandResponse>.Error(result.Messages!);

            session.Store(role);
            return FeatureObjectResultModel<RemoveRolePermissionCommandResponse>.Ok(
                new RemoveRolePermissionCommandResponse());
        }
    }
}
```

- [ ] **Step 4: Build al**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | grep "error" | head -20
```

- [ ] **Step 5: Commit**

```bash
git add IAM.Api/Domains/Roles/Features/Commands/CreateRole.cs \
  IAM.Api/Domains/Roles/Features/Commands/AddRolePermission.cs \
  IAM.Api/Domains/Roles/Features/Commands/RemoveRolePermission.cs
git commit -m "feat(iam): migrate role command handlers to Marten"
```

---

### Task 7: Role Query Handlers + Final Build

**Files:**
- Modify: `IAM.Api/Domains/Roles/Features/Queries/GetAllRoles.cs`
- Modify: `IAM.Api/Domains/Roles/Features/Queries/GetRoleById.cs`

- [ ] **Step 1: GetAllRoles handler'ı güncelle**

`IAM.Api/Domains/Roles/Features/Queries/GetAllRoles.cs`:

```csharp
using PaymentGatewayApi.Modules.IAM.Roles;

namespace PaymentGatewayApi.Modules.IAM.Roles.Features.Queries;

public static class GetAllRoles
{
    public class GetAllRolesQuery { }

    public class RoleListItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsSystem { get; set; }
    }

    public class GetAllRolesHandler
    {
        public async Task<FeatureObjectResultModel<List<RoleListItem>>> Handle(
            GetAllRolesQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var roles = await session.Query<Role>().ToListAsync(ct);
            var result = roles.Select(x => new RoleListItem
            {
                Id = x.Id,
                Name = x.Name.Value,
                IsSystem = x.IsSystem
            }).ToList();

            return FeatureObjectResultModel<List<RoleListItem>>.Ok(result);
        }
    }
}
```

- [ ] **Step 2: GetRoleById handler'ı güncelle**

`IAM.Api/Domains/Roles/Features/Queries/GetRoleById.cs`:

```csharp
using PaymentGatewayApi.Modules.IAM.Roles;

namespace PaymentGatewayApi.Modules.IAM.Roles.Features.Queries;

public static class GetRoleById
{
    public class GetRoleByIdQuery
    {
        public required Guid RoleId { get; set; }
    }

    public class GetRoleByIdResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsSystem { get; set; }
        public List<PagePermissionItem> Permissions { get; set; } = [];
    }

    public class PagePermissionItem
    {
        public Guid Id { get; set; }
        public string PageRoute { get; set; }
        public List<string> Actions { get; set; } = [];
    }

    public class GetRoleByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetRoleByIdResponse>> Handle(
            GetRoleByIdQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var role = await session.LoadAsync<Role>(query.RoleId, ct);
            if (role is null)
                return FeatureObjectResultModel<GetRoleByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetRoleByIdResponse>.Ok(new GetRoleByIdResponse
            {
                Id = role.Id,
                Name = role.Name.Value,
                IsSystem = role.IsSystem,
                Permissions = role.Permissions.Select(p => new PagePermissionItem
                {
                    Id = p.Id,
                    PageRoute = p.PageRoute,
                    Actions = p.Actions.Select(a => a.Action).ToList()
                }).ToList()
            });
        }
    }
}
```

- [ ] **Step 3: Final build — sıfır hata olmalı**

```bash
dotnet build IAM.Api/IAM.Api.csproj 2>&1 | tail -5
```

Beklenen çıktı:
```
Build succeeded.
    0 Error(s)
    0 Warning(s)
```

- [ ] **Step 4: Commit**

```bash
git add IAM.Api/Domains/Roles/Features/Queries/GetAllRoles.cs \
  IAM.Api/Domains/Roles/Features/Queries/GetRoleById.cs
git commit -m "feat(iam): migrate role query handlers to Marten, complete EF Core removal"
```