# DbContext, Program.cs & WebhookDispatcher Fix Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tüm servisleri derlenebilir hale getir: eksik DbContext'leri yarat, Program.cs dosyalarını wirele, WebhookDispatcher'ı implement et ve PaymentProcessing'in yeni ReadModel'leri subscribe etmesini sağla.

**Architecture:** Üç ayrı EF Core DbContext (MerchantManagement, BankIntegration, CommissionManagement) — her servisin kendi PostgreSQL şeması var. PaymentProcessing, cross-servis verileri Marten ReadModel'ler üzerinden okur; bu ReadModel'ler Wolverine/RabbitMQ event handler'ları tarafından doldurulur.

**Tech Stack:** .NET 10, Wolverine 5.38, EF Core 10 (Npgsql 10.0.1), Marten 8.x, RabbitMQ, Aspire ServiceDefaults

---

## Dosya Haritası

| Oluşturulacak / Düzenlenecek | Sorumluluk |
|---|---|
| `MerchantManagement.Api/Infrastructure/MerchantManagementContext.cs` | Merchant, ApiKey, MerchantBankAccount EF mapping |
| `MerchantManagement.Api/Program.cs` | AddDbContext kaydı ekleme |
| `BankIntegration.Api/Infrastructure/BankIntegrationContext.cs` | Bank, MerchantBank EF mapping |
| `BankIntegration.Api/GlobalUsings.cs` | EF Core global using ekleme |
| `BankIntegration.Api/Program.cs` | Tam servis wiring |
| `CommissionManagement.Api/CommissionManagement.Api.csproj` | NuGet paket ekleme |
| `CommissionManagement.Api/Infrastructure/CommissionManagementContext.cs` | BankCommission, MerchantCommission EF mapping |
| `CommissionManagement.Api/Dependencies/DependencyExtensions.cs` | Scrutor scan |
| `CommissionManagement.Api/GlobalUsings.cs` | Global using'leri tamamlama |
| `CommissionManagement.Api/Program.cs` | Tam servis wiring |
| `CommissionManagement.Api/CommissionManagement/BankCommissions/Features/Commands/DefineBankCommission.cs` | `db` parametresi ekleme |
| `CommissionManagement.Api/CommissionManagement/BankCommissions/Features/Commands/UpdateBankCommissionRate.cs` | `db` parametresi ekleme |
| `PaymentProcessing.Api/Program.cs` | Marten schema + RabbitMQ subscription ekleme |
| `PaymentProcessing.Api/PaymentProcessing/PaymentTransactions/Features/Dispatchers/WebhookDispatcher.cs` | MerchantSummary lookup + HTTP dispatch |

---

### Task 1: MerchantManagementContext — Oluştur ve kaydet

**Files:**
- Create: `MerchantManagement.Api/Infrastructure/MerchantManagementContext.cs`
- Modify: `MerchantManagement.Api/Program.cs` (AddDbContext + UseEntityFrameworkCoreTransactions referansı)

- [ ] **Step 1: Create MerchantManagementContext.cs**

```csharp
using MerchantManagement.Api.Modules.MerchantManagement.Merchants;
using MerchantManagement.Api.Modules.MerchantManagement.Merchants.Entities;
using MerchantManagement.Api.Modules.MerchantManagement.Merchants.Enums;
using MerchantManagement.Api.Modules.MerchantManagement.Merchants.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace MerchantManagement.Api.Infrastructure;

public class MerchantManagementContext(DbContextOptions<MerchantManagementContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("merchantManagement");

        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name)
                .HasConversion(n => n.Value, v => MerchantName.FromPersistence(v))
                .HasColumnName("name");
            entity.Property(m => m.Status)
                .HasConversion<int>()
                .HasColumnName("status");
            entity.OwnsOne(m => m.ContactInfo, owned =>
            {
                owned.Property(c => c.Email).HasColumnName("email");
                owned.Property(c => c.Phone).HasColumnName("phone");
            });
            entity.OwnsOne(m => m.Address, owned =>
            {
                owned.Property(a => a.Country).HasColumnName("country");
                owned.Property(a => a.City).HasColumnName("city");
            });
            entity.Property(m => m.Mcc)
                .HasConversion(m => m.Value, v => Mcc.FromPersistence(v))
                .HasColumnName("mcc");
            entity.Property(m => m.WebhookUrl)
                .HasConversion(w => w.Value, v => WebhookUrl.FromPersistence(v))
                .HasColumnName("webhook_url");

            entity.HasMany<ApiKey>("_apiKeys")
                .WithOne()
                .HasForeignKey("MerchantId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany<MerchantBankAccount>("_bankAccounts")
                .WithOne()
                .HasForeignKey("MerchantId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(k => k.Id);
            entity.Property(k => k.KeyValue)
                .HasConversion(k => k.Hash, v => ApiKeyValue.FromHash(v))
                .HasColumnName("key_hash");
            entity.Property(k => k.Status)
                .HasConversion<int>()
                .HasColumnName("status");
            entity.Property(k => k.ExpiresAt).HasColumnName("expires_at");
            entity.Property(k => k.RevokedAt).HasColumnName("revoked_at");
        });

        modelBuilder.Entity<MerchantBankAccount>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Iban).HasColumnName("iban");
            entity.Property(b => b.SwiftCode).HasColumnName("swift_code");
            entity.Property(b => b.BankName).HasColumnName("bank_name");
            entity.Property(b => b.Currency)
                .HasConversion(c => c.Code, v => Currency.FromPersistence(v))
                .HasColumnName("currency");
        });
    }
}
```

- [ ] **Step 2: MerchantManagement.Api/Program.cs — AddDbContext ekle**

`var connString = builder.Configuration.GetConnectionString("merchantDb")!;` satırının hemen altına, `builder.Host.UseWolverine` çağrısından önce şunu ekle:

```csharp
builder.Services.AddDbContext<MerchantManagementContext>(opts =>
    opts.UseNpgsql(connString));
```

`GlobalUsings.cs`'e `using MerchantManagement.Api.Infrastructure;` eklenmesi gerekmez çünkü Program.cs top-level file'dır ve using doğrudan eklenir. Program.cs dosyasının başına:
```csharp
using MerchantManagement.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
```

- [ ] **Step 3: Build kontrolü**

```bash
cd /Users/macbook/Desktop/PaymentGateway
dotnet build MerchantManagement.Api/MerchantManagement.Api.csproj 2>&1 | grep -E "error|Error|warning CS0246"
```

Expected: 0 error.

- [ ] **Step 4: Commit**

```bash
git add MerchantManagement.Api/Infrastructure/MerchantManagementContext.cs MerchantManagement.Api/Program.cs
git commit -m "feat(merchant): create MerchantManagementContext, register DbContext in Program"
```

---

### Task 2: BankIntegrationContext — Oluştur ve GlobalUsings güncelle

**Files:**
- Create: `BankIntegration.Api/Infrastructure/BankIntegrationContext.cs`
- Modify: `BankIntegration.Api/GlobalUsings.cs`

- [ ] **Step 1: Create BankIntegrationContext.cs**

```csharp
using BankIntegration.Api.BankIntegration.Banks.ValueObjects;
using BankIntegration.Api.Domains.Banks;
using BankIntegration.Api.Domains.Banks.Enums;
using Microsoft.EntityFrameworkCore;
using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks;
using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Enums;

namespace BankIntegration.Api.Infrastructure;

public class BankIntegrationContext(DbContextOptions<BankIntegrationContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("bankIntegration");

        modelBuilder.Entity<Bank>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Name)
                .HasConversion(n => n.Value, v => BankName.FromPersistence(v))
                .HasColumnName("name");
            entity.Property(b => b.Priority)
                .HasConversion(p => p.Value, v => BankPriority.FromPersistence(v))
                .HasColumnName("priority");
            entity.Property(b => b.ApiUrl)
                .HasConversion(u => u.Value, v => BankApiUrl.FromPersistence(v))
                .HasColumnName("api_url");
            entity.Property(b => b.Status)
                .HasConversion<int>()
                .HasColumnName("status");
            entity.Property(b => b.IcaMemberId).HasColumnName("ica_member_id");
            entity.PrimitiveCollection<string>("_supportedCurrencies")
                .HasColumnName("supported_currencies");
        });

        modelBuilder.Entity<MerchantBank>(entity =>
        {
            entity.HasKey(mb => mb.Id);
            entity.Property(mb => mb.MerchantId).HasColumnName("merchant_id");
            entity.Property(mb => mb.BankId).HasColumnName("bank_id");
            entity.Property(mb => mb.MerchantCode).HasColumnName("merchant_code");
            entity.Property(mb => mb.TerminalCode).HasColumnName("terminal_code");
            entity.Property(mb => mb.Status)
                .HasConversion<int>()
                .HasColumnName("status");
        });
    }
}
```

- [ ] **Step 2: BankIntegration.Api/GlobalUsings.cs — EF Core using ekle**

Mevcut dosyaya şu satırları ekle:
```csharp
global using Microsoft.EntityFrameworkCore;
global using Wolverine.EntityFrameworkCore;
global using BankIntegration.Api.Infrastructure;
```

- [ ] **Step 3: Build kontrolü**

```bash
dotnet build BankIntegration.Api/BankIntegration.Api.csproj 2>&1 | grep -E "^.*error"
```

Expected: 0 error (sadece Program.cs "Hello World" build olacak).

- [ ] **Step 4: Commit**

```bash
git add BankIntegration.Api/Infrastructure/BankIntegrationContext.cs BankIntegration.Api/GlobalUsings.cs
git commit -m "feat(bank-integration): create BankIntegrationContext with EF Core mappings"
```

---

### Task 3: BankIntegration.Api/Program.cs — Tam wiring

**Files:**
- Modify: `BankIntegration.Api/Program.cs`

- [ ] **Step 1: Program.cs'i yeniden yaz**

```csharp
using BankIntegration.Api.BankIntegration.Banks.Features.Endpoints;
using BankIntegration.Api.BankIntegration.MerchantBanks.Features.Endpoints;
using BankIntegration.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();
builder.Services.LoadCurrentUser();

var bankIntDb = builder.Configuration.GetConnectionString("bankIntegrationDb")!;
var rabbitMq  = builder.Configuration.GetConnectionString("rabbitmq")!;

builder.Services.AddDbContext<BankIntegrationContext>(opts =>
    opts.UseNpgsql(bankIntDb));

builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    opts.PublishMessage<MerchantBankSynced>().ToRabbitExchange("bank.merchant-bank-synced");
    opts.PublishMessage<BankRouteSynced>().ToRabbitExchange("bank.route-synced");
});

var app = builder.Build();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapBankEndpoints();
app.MapMerchantBankEndpoints();
app.Run();
```

- [ ] **Step 2: Build kontrolü**

```bash
dotnet build BankIntegration.Api/BankIntegration.Api.csproj 2>&1 | grep -E "^.*error"
```

Expected: 0 error.

- [ ] **Step 3: Commit**

```bash
git add BankIntegration.Api/Program.cs
git commit -m "feat(bank-integration): wire up Program.cs — EF Core, Wolverine, RabbitMQ publishers"
```

---

### Task 4: CommissionManagement.Api — csproj, DbContext, Dependencies, GlobalUsings

**Files:**
- Modify: `CommissionManagement.Api/CommissionManagement.Api.csproj`
- Create: `CommissionManagement.Api/Infrastructure/CommissionManagementContext.cs`
- Create: `CommissionManagement.Api/Dependencies/DependencyExtensions.cs`
- Modify: `CommissionManagement.Api/GlobalUsings.cs`

- [ ] **Step 1: CommissionManagement.Api.csproj — NuGet paketleri ekle**

`<ItemGroup>` paket bloğunu şu şekilde güncelle (mevcut içeriği koru, yenilerini ekle):

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <ProjectReference Include="..\Common\Common.csproj" />
    <ProjectReference Include="..\ServiceDefaults\ServiceDefaults.csproj" />
    <ProjectReference Include="..\PaymentGateway.SharedContracts\PaymentGateway.SharedContracts.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.1" />
    <PackageReference Include="Scrutor" Version="7.0.0" />
    <PackageReference Include="WolverineFx" Version="5.38.0" />
    <PackageReference Include="WolverineFx.EntityFrameworkCore" Version="5.38.0" />
    <PackageReference Include="WolverineFx.Http" Version="5.38.0" />
    <PackageReference Include="WolverineFx.Postgresql" Version="5.38.0" />
    <PackageReference Include="WolverineFx.RabbitMQ" Version="5.38.0" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

- [ ] **Step 2: Create CommissionManagementContext.cs**

```csharp
using CommissionManagement.Api.CommissionManagement.BankCommissions;
using CommissionManagement.Api.CommissionManagement.BankCommissions.ValueObjects;
using CommissionManagement.Api.CommissionManagement.MerchantCommissions;
using Microsoft.EntityFrameworkCore;

namespace CommissionManagement.Api.Infrastructure;

public class CommissionManagementContext(DbContextOptions<CommissionManagementContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("commissionManagement");

        modelBuilder.Entity<BankCommission>(entity =>
        {
            entity.HasKey(bc => bc.Id);
            entity.Property(bc => bc.BankId).HasColumnName("bank_id");
            entity.OwnsOne(bc => bc.Criteria, owned =>
            {
                owned.Property(c => c.CardBrand)
                    .HasConversion<int>()
                    .HasColumnName("card_brand");
                owned.Property(c => c.CardType)
                    .HasConversion<int>()
                    .HasColumnName("card_type");
                owned.Property(c => c.TransactionRegion)
                    .HasConversion<int>()
                    .HasColumnName("transaction_region");
            });
            entity.Property(bc => bc.Rate)
                .HasConversion(r => r.Value, v => CommissionRate.FromPersistence(v))
                .HasColumnName("rate");
        });

        modelBuilder.Entity<MerchantCommission>(entity =>
        {
            entity.HasKey(mc => mc.Id);
            entity.Property(mc => mc.MerchantId).HasColumnName("merchant_id");
            entity.Property(mc => mc.BankCommissionId).HasColumnName("bank_commission_id");
            entity.OwnsOne(mc => mc.Criteria, owned =>
            {
                owned.Property(c => c.CardBrand)
                    .HasConversion<int>()
                    .HasColumnName("card_brand");
                owned.Property(c => c.CardType)
                    .HasConversion<int>()
                    .HasColumnName("card_type");
                owned.Property(c => c.TransactionRegion)
                    .HasConversion<int>()
                    .HasColumnName("transaction_region");
            });
            entity.Property(mc => mc.Rate)
                .HasConversion(r => r.Value, v => CommissionRate.FromPersistence(v))
                .HasColumnName("rate");
        });
    }
}
```

- [ ] **Step 3: Create DependencyExtensions.cs**

```csharp
using Common.Dependencies.Models;
using Scrutor;

namespace CommissionManagement.Api.Dependencies;

public static class DependencyExtensions
{
    public static void AddAllDependencies(this IServiceCollection serviceCollection)
    {
        serviceCollection.Scan(scan => scan
            .FromApplicationDependencies()
            .AddClasses(classes => classes.AssignableTo<ITransientDependency>()).AsImplementedInterfaces().WithTransientLifetime()
            .AddClasses(classes => classes.AssignableTo<IScopedDependency>()).AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo<ISingletonDependency>()).AsImplementedInterfaces().WithSingletonLifetime()
        );
    }
}
```

- [ ] **Step 4: GlobalUsings.cs — tamamla**

```csharp
global using CommissionManagement.Api.CommissionManagement.BankCommissions.ValueObjects;
global using Common;
global using Common.Utils.Constants;
global using PaymentGateway.SharedContracts.CommissionEvents;
global using Microsoft.EntityFrameworkCore;
global using System.Reflection;
global using Wolverine;
global using Wolverine.EntityFrameworkCore;
global using Wolverine.Attributes;
global using Common.Dependencies.Models;
global using System.Text.Json;
global using Common.Auths;
global using CommissionManagement.Api.Dependencies;
global using CommissionManagement.Api.Infrastructure;
global using CommissionManagement.Api.CommissionManagement.BankCommissions;
global using CommissionManagement.Api.CommissionManagement.MerchantCommissions;
```

- [ ] **Step 5: Build kontrolü (sadece paket restore)**

```bash
dotnet restore CommissionManagement.Api/CommissionManagement.Api.csproj
dotnet build CommissionManagement.Api/CommissionManagement.Api.csproj 2>&1 | grep -E "^.*error"
```

Expected: Derleme başarılı (Program.cs hâlâ Hello World, sorun değil).

- [ ] **Step 6: Commit**

```bash
git add CommissionManagement.Api/CommissionManagement.Api.csproj \
        CommissionManagement.Api/Infrastructure/CommissionManagementContext.cs \
        CommissionManagement.Api/Dependencies/DependencyExtensions.cs \
        CommissionManagement.Api/GlobalUsings.cs
git commit -m "feat(commission): add NuGet packages, create CommissionManagementContext, DependencyExtensions"
```

---

### Task 5: Fix DefineBankCommission ve UpdateBankCommissionRate

**Files:**
- Modify: `CommissionManagement.Api/CommissionManagement/BankCommissions/Features/Commands/DefineBankCommission.cs`
- Modify: `CommissionManagement.Api/CommissionManagement/BankCommissions/Features/Commands/UpdateBankCommissionRate.cs`

- [ ] **Step 1: DefineBankCommission — `db` parametresi ekle**

`DefineBankCommissionHandler.Handle` metodunu şu şekilde güncelle:

```csharp
[Transactional]
public class DefineBankCommissionHandler
{
    public async Task<FeatureObjectResultModel<DefineBankCommissionResponse>> Handle(
        DefineBankCommissionCommand cmd,
        CommissionManagementContext db,
        IMessageBus bus,
        CancellationToken ct)
    {
        var rateResult = CommissionRate.Create(cmd.Rate);
        if (!rateResult.IsSuccess)
            return FeatureObjectResultModel<DefineBankCommissionResponse>.Error(rateResult.Messages!);

        var criteria   = new CommissionCriteria(cmd.CardBrand, cmd.CardType, cmd.TransactionRegion);
        var commission = BankCommission.Define(cmd.BankId, criteria, rateResult.Data!);
        await db.Set<BankCommission>().AddAsync(commission, ct);

        await bus.PublishAsync(new BankCommissionSynced(
            commission.Id,
            commission.BankId,
            commission.Criteria.CardBrand.ToString(),
            commission.Criteria.CardType.ToString(),
            commission.Criteria.TransactionRegion.ToString(),
            commission.Rate.Value,
            DateTime.UtcNow));

        return FeatureObjectResultModel<DefineBankCommissionResponse>.Ok(
            new DefineBankCommissionResponse { Id = commission.Id });
    }
}
```

- [ ] **Step 2: UpdateBankCommissionRate — `db` parametresi ekle**

`UpdateBankCommissionRateHandler.Handle` metodunu şu şekilde güncelle:

```csharp
[Transactional]
public class UpdateBankCommissionRateHandler
{
    public async Task<FeatureObjectResultModel<UpdateBankCommissionRateCommandResponse>> Handle(
        UpdateBankCommissionRateCommand cmd,
        CommissionManagementContext db,
        IMessageBus bus,
        CancellationToken ct)
    {
        var commission = await db.Set<BankCommission>().FirstOrDefaultAsync(x => x.Id == cmd.CommissionId, ct);
        if (commission is null)
            return FeatureObjectResultModel<UpdateBankCommissionRateCommandResponse>.Error(new MessageItem
            {
                Table = nameof(BankCommission),
                Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
            });

        var rateResult = CommissionRate.Create(cmd.NewRate);
        if (!rateResult.IsSuccess)
            return FeatureObjectResultModel<UpdateBankCommissionRateCommandResponse>.Error(rateResult.Messages!);

        commission.UpdateRate(rateResult.Data!);

        await bus.PublishAsync(new BankCommissionRateUpdated(commission.Id, cmd.NewRate, DateTime.UtcNow));
        return FeatureObjectResultModel<UpdateBankCommissionRateCommandResponse>.Ok(
            new UpdateBankCommissionRateCommandResponse());
    }
}
```

- [ ] **Step 3: Build kontrolü**

```bash
dotnet build CommissionManagement.Api/CommissionManagement.Api.csproj 2>&1 | grep -E "^.*error"
```

Expected: 0 error.

- [ ] **Step 4: Commit**

```bash
git add CommissionManagement.Api/CommissionManagement/BankCommissions/Features/Commands/DefineBankCommission.cs \
        CommissionManagement.Api/CommissionManagement/BankCommissions/Features/Commands/UpdateBankCommissionRate.cs
git commit -m "fix(commission): add CommissionManagementContext db param to DefineBankCommission and UpdateBankCommissionRate handlers"
```

---

### Task 6: CommissionManagement.Api/Program.cs — Tam wiring

**Files:**
- Modify: `CommissionManagement.Api/Program.cs`

- [ ] **Step 1: Program.cs'i yeniden yaz**

```csharp
using CommissionManagement.Api.CommissionManagement.BankCommissions.Features.Endpoints;
using CommissionManagement.Api.CommissionManagement.MerchantCommissions.Features.Endpoints;
using CommissionManagement.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();
builder.Services.LoadCurrentUser();

var commissionDb = builder.Configuration.GetConnectionString("commissionDb")!;
var rabbitMq     = builder.Configuration.GetConnectionString("rabbitmq")!;

builder.Services.AddDbContext<CommissionManagementContext>(opts =>
    opts.UseNpgsql(commissionDb));

builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

    opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

    opts.PublishMessage<BankCommissionSynced>().ToRabbitExchange("commission.bank-commission-synced");
    opts.PublishMessage<BankCommissionRateUpdated>().ToRabbitExchange("commission.bank-commission-rate-updated");
    opts.PublishMessage<MerchantCommissionSynced>().ToRabbitExchange("commission.merchant-commission-synced");
    opts.PublishMessage<MerchantCommissionRateUpdated>().ToRabbitExchange("commission.merchant-commission-rate-updated");
});

var app = builder.Build();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapBankCommissionEndpoints();
app.MapMerchantCommissionEndpoints();
app.Run();
```

`CommissionManagement.Api` içinde `LoadCurrentUser()` extension method yok. `BankIntegration.Api`'deki `AuthExtensions` dosyasından kopyalanacak:

- [ ] **Step 2: Create CommissionManagement.Api/Auths/AuthExtensions.cs**

```csharp
using Common.Auths;

namespace CommissionManagement.Api.Auths;

public static class AuthExtensions
{
    public static void LoadCurrentUser(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ICurrentUser>(provider =>
        {
            var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var authHeader = httpContext?.Request.Headers.Authorization.FirstOrDefault();

            if (string.IsNullOrEmpty(authHeader))
                return new CurrentUser();

            try
            {
                return CurrentUser.Load(authHeader);
            }
            catch
            {
                return new CurrentUser();
            }
        });
    }
}
```

GlobalUsings.cs'e ekle:
```csharp
global using CommissionManagement.Api.Auths;
```

- [ ] **Step 3: GlobalUsings.cs'e `AddGlobalExceptionHandler` için exception namespace ekle**

CommissionManagement.Api içinde exception handler yok. `Exceptions/` klasörü eksik. BankIntegration.Api'den kopyala:

Create `CommissionManagement.Api/Exceptions/GlobalExceptionExtension.cs`:
```csharp
namespace CommissionManagement.Api.Exceptions;

public static class GlobalExceptionExtension
{
    public static void AddGlobalExceptionHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
    }
}
```

Create `CommissionManagement.Api/Exceptions/GlobalExceptionHandler.cs` — BankIntegration.Api'deki aynı dosyayı baz al.

- [ ] **Step 4: BankIntegration.Api/Exceptions/ dosyalarına bak**

```bash
cat /Users/macbook/Desktop/PaymentGateway/BankIntegration.Api/Exceptions/GlobalExceptionExtension.cs
cat /Users/macbook/Desktop/PaymentGateway/BankIntegration.Api/Exceptions/GlobalExceptionHandler.cs
```

Bu dosyaları `CommissionManagement.Api/Exceptions/` altında namespace'i `CommissionManagement.Api.Exceptions` olarak değiştirerek oluştur.

- [ ] **Step 5: Build kontrolü**

```bash
dotnet build CommissionManagement.Api/CommissionManagement.Api.csproj 2>&1 | grep -E "^.*error"
```

Expected: 0 error.

- [ ] **Step 6: Commit**

```bash
git add CommissionManagement.Api/Program.cs \
        CommissionManagement.Api/Auths/AuthExtensions.cs \
        CommissionManagement.Api/Exceptions/
git commit -m "feat(commission): wire up Program.cs — EF Core, Wolverine, RabbitMQ publishers"
```

---

### Task 7: PaymentProcessing.Api/Program.cs — Marten schema + RabbitMQ subscription

**Files:**
- Modify: `PaymentProcessing.Api/Program.cs`

- [ ] **Step 1: Marten şema kayıtlarını ekle**

`opts.Schema.For<BinRecord>()` bloğundan sonra şunları ekle:

```csharp
opts.Schema.For<MerchantBankSummary>()
    .Index(mb => mb.MerchantId)
    .Index(mb => mb.BankId);
opts.Schema.For<BankCommissionSummary>()
    .Index(bc => bc.BankId);
opts.Schema.For<MerchantCommissionSummary>()
    .Index(mc => mc.MerchantId)
    .Index(mc => mc.BankCommissionId);
```

- [ ] **Step 2: RabbitMQ subscription'larını ekle**

`opts.ListenToRabbitQueue("payment-processing.bank-routes");` satırından sonra:

```csharp
// Subscribe to merchant-bank assignments
transport.BindExchange("bank.merchant-bank-synced", ExchangeType.Fanout)
    .ToQueue("payment-processing.merchant-bank-events");
opts.ListenToRabbitQueue("payment-processing.merchant-bank-events");

// Subscribe to commission data
transport.BindExchange("commission.bank-commission-synced", ExchangeType.Fanout)
    .ToQueue("payment-processing.commission-events");
transport.BindExchange("commission.bank-commission-rate-updated", ExchangeType.Fanout)
    .ToQueue("payment-processing.commission-events");
transport.BindExchange("commission.merchant-commission-synced", ExchangeType.Fanout)
    .ToQueue("payment-processing.commission-events");
transport.BindExchange("commission.merchant-commission-rate-updated", ExchangeType.Fanout)
    .ToQueue("payment-processing.commission-events");
opts.ListenToRabbitQueue("payment-processing.commission-events");
```

- [ ] **Step 3: Build kontrolü**

```bash
dotnet build PaymentProcessing.Api/PaymentProcessing.Api.csproj 2>&1 | grep -E "^.*error"
```

Expected: 0 error.

- [ ] **Step 4: Commit**

```bash
git add PaymentProcessing.Api/Program.cs
git commit -m "feat(payment): add Marten schema registrations and RabbitMQ subscriptions for commission and merchant-bank events"
```

---

### Task 8: WebhookDispatcher — MerchantSummary lookup + HTTP dispatch

**Files:**
- Modify: `PaymentProcessing.Api/PaymentProcessing/PaymentTransactions/Features/Dispatchers/WebhookDispatcher.cs`

- [ ] **Step 1: WebhookDispatcher'ı implement et**

```csharp
using Marten;
using PaymentProcessing.Api.PaymentProcessing.Merchants;
using PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Events;
using System.Net.Http.Json;

namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Features.Dispatchers;

public class WebhookDispatcher
{
    public Task Handle(PaymentApproved evt, IQuerySession session, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: true, evt.ResultCode, message: null, evt.BankTransactionId,
            session, httpClientFactory, logger, ct);

    public Task Handle(PaymentDeclined evt, IQuerySession session, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: false, evt.BankResponseCode, evt.BankMessage, bankTransactionId: null,
            session, httpClientFactory, logger, ct);

    public Task Handle(PaymentFailed evt, IQuerySession session, IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct) =>
        SendWebhookAsync(evt.MerchantId, evt.TransactionId, evt.OrderId,
            isApproved: false, resultCode: "99", evt.Reason, bankTransactionId: null,
            session, httpClientFactory, logger, ct);

    private async Task SendWebhookAsync(
        Guid merchantId, Guid transactionId, string orderId,
        bool isApproved, string resultCode, string? message, string? bankTransactionId,
        IQuerySession session,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger, CancellationToken ct)
    {
        var merchant = await session.LoadAsync<MerchantSummary>(merchantId, ct);
        if (merchant?.WebhookUrl is null)
        {
            logger.LogWarning("No webhook URL for merchant {MerchantId}", merchantId);
            return;
        }

        var payload = new
        {
            transactionId,
            orderId,
            isApproved,
            resultCode,
            message,
            bankTransactionId
        };

        try
        {
            var client = httpClientFactory.CreateClient("webhook");
            var response = await client.PostAsJsonAsync(merchant.WebhookUrl, payload, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Webhook failed for merchant {MerchantId}: {StatusCode}",
                    merchantId, response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook error for merchant {MerchantId}", merchantId);
        }
    }
}
```

- [ ] **Step 2: Build kontrolü**

```bash
dotnet build PaymentProcessing.Api/PaymentProcessing.Api.csproj 2>&1 | grep -E "^.*error"
```

Expected: 0 error.

- [ ] **Step 3: Full solution build**

```bash
cd /Users/macbook/Desktop/PaymentGateway
dotnet build PaymentGateway.sln 2>&1 | tail -20
```

Expected: 0 error across all projects.

- [ ] **Step 4: Commit**

```bash
git add PaymentProcessing.Api/PaymentProcessing/PaymentTransactions/Features/Dispatchers/WebhookDispatcher.cs
git commit -m "feat(payment): implement WebhookDispatcher with MerchantSummary Marten lookup"
```

---

## Özet

Tüm task'lar tamamlandığında:
- `MerchantManagement.Api` ✅ compiles, EF Core + Wolverine tam çalışır
- `BankIntegration.Api` ✅ compiles, `MerchantBankSynced` ve `BankRouteSynced` yayınlar
- `CommissionManagement.Api` ✅ compiles, tüm commission event'lerini yayınlar
- `PaymentProcessing.Api` ✅ commission + merchant-bank event'lerini subscribe eder, BankSelector Marten ReadModel'leri kullanır, WebhookDispatcher çalışır