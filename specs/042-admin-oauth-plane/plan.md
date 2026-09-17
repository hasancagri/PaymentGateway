# Admin OAuth Düzlemi (G3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Identity.Server'a insan login + `authorization_code`+PKCE akışı ekle; tek seed public
istemci (`external-admin-agent`) ile Claude Desktop'ın admin olarak token alabilmesini sağla.

**Architecture:** ECommerceWithAgentFramework'ün 061 (DCR/OAuth altyapısı) + 070 (seed admin
istemcisi + loopback muafiyeti) desenlerinin PG'ye uyarlanmış, sadeleştirilmiş (RBAC/DCR/consent
YOK — tek admin, tek seed istemci, Implicit consent) birleşimi. Mevcut `client_credentials` akışı
DOKUNULMADAN korunur; `/connect/token` aynı uçta grant-type'a göre dallanır.

**Tech Stack:** OpenIddict (AspNetCore host), ASP.NET Identity (zaten kurulu `ApplicationUser`/
`ApplicationDbContext`), Razor Pages (zaten `AddRazorPages()` kayıtlı — aktivasyon sayfası için).

**Spec:** `specs/042-admin-oauth-plane/spec.md`

## Global Constraints

- Mevcut `client_credentials` davranışı (merchant/M2M istemciler) DEĞİŞMEZ — regresyon yasak.
- `AdminPlaneOnly` policy'sine (Common) DOKUNULMAZ — negatif kontrol (`merchant_id` claim yokluğu)
  zaten insan token'ını kabul eder (spec FR-007).
- Consent ekranı YOK (seed istemci → `ConsentType=Implicit`, spec FR-004).
- RBAC/rol tablosu/self-servis kayıt YOK (spec Assumptions — tek bootstrap admin, YAGNI).
- Kod yorumları + XML doküman Türkçe (proje konvansiyonu).
- `IConfiguration`'dan doğrudan okuma YASAK — Options pattern (proje konvansiyonu, `BootstrapAdmin`
  zaten bu deseni izler).

---

### Task 1: BootstrapAdmin option + idempotent seed

**Files:**
- Create: `src/others/Identity.Server/Options/BootstrapAdmin.cs`
- Modify: `src/others/Identity.Server/Connect/SeedHostedService.cs`
- Modify: `src/others/Identity.Server/Program.cs:26-29` (bind option)

**Interfaces:**
- Consumes: `ApplicationUser` (mevcut, `Data/ApplicationUser.cs`), `UserManager<ApplicationUser>`.
- Produces: config boşsa hiç kullanıcı yok; doluysa tek `ApplicationUser` (email=`UserName`).

- [ ] **Step 1: BootstrapAdmin option'ı yaz**

```csharp
namespace Identity.Server.Options;

// G3: acilista seed edilen tek admin kimligi — section "BootstrapAdmin". Email/parola boşken
// (config'te tanımsız) kullanıcı oluşturma atlanır; bu yüzden alanlar zorunlu (Required) DEĞİL.
// Placeholder: kullanıcı `dotnet user-secrets set BootstrapAdmin:Email/Password` ile kendi
// değerini girer.
public class BootstrapAdmin
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}
```

- [ ] **Step 2: Program.cs'e option bağlama ekle**

`src/others/Identity.Server/Program.cs` içinde, `builder.Services.AddHostedService<SeedHostedService>();`
satırından HEMEN ÖNCE ekle:

```csharp
// G3: bootstrap admin — email/parola boşsa seed atlanır (Options/BootstrapAdmin.cs).
builder.Services.AddOptions<Identity.Server.Options.BootstrapAdmin>()
    .BindConfiguration(nameof(Identity.Server.Options.BootstrapAdmin));
builder.Services.AddSingleton<Identity.Server.Options.BootstrapAdmin>(sp =>
    sp.GetRequiredService<IOptions<Identity.Server.Options.BootstrapAdmin>>().Value);
```

Dosya başına `using Microsoft.Extensions.Options;` ekle (henüz yoksa).

- [ ] **Step 3: SeedHostedService.cs'e bootstrap admin seed'i ekle**

`SeedHostedService.cs`'in mevcut `StartAsync` metodunun SONUNA (istemci+scope seed döngüsünden
sonra) ekle:

```csharp
        // G3: bootstrap admin — yalnız config doluysa VE kullanıcı yoksa oluşturulur (idempotent;
        // sonradan admin'in değiştirdiği parola ezilmez).
        var bootstrapAdmin = scope.ServiceProvider.GetRequiredService<Identity.Server.Options.BootstrapAdmin>();
        if (!string.IsNullOrWhiteSpace(bootstrapAdmin.Email) && !string.IsNullOrWhiteSpace(bootstrapAdmin.Password))
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByNameAsync(bootstrapAdmin.Email) is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = bootstrapAdmin.Email,
                    Email = bootstrapAdmin.Email,
                    EmailConfirmed = true,
                };
                var created = await userManager.CreateAsync(admin, bootstrapAdmin.Password);
                if (!created.Succeeded)
                    throw new InvalidOperationException(
                        $"Bootstrap admin oluşturulamadı: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            }
        }
```

`SeedHostedService.cs`'in `using` bloğuna (varsa) `Microsoft.AspNetCore.Identity` zaten mevcut
olmalı (`UserManager<ApplicationUser>` param tipi); değilse ekle.

- [ ] **Step 4: appsettings.Development.json'a placeholder ekle**

`src/others/Identity.Server/appsettings.Development.json`'a ekle (gerçek değeri kullanıcı
`dotnet user-secrets` ile girecek — bu placeholder yalnız section'ın var olduğunu gösterir, boş
kalınca seed atlanır):

```json
"BootstrapAdmin": {
  "Email": "",
  "Password": ""
}
```

- [ ] **Step 5: Canlı doğrula**

```bash
dotnet build src/others/Identity.Server/Identity.Server.csproj
dotnet user-secrets set "BootstrapAdmin:Email" "admin@test.local" --project src/others/Identity.Server
dotnet user-secrets set "BootstrapAdmin:Password" "Test123!" --project src/others/Identity.Server
dotnet run --project src/others/Identity.Server/Identity.Server.csproj
```

Beklenen: açılış hatasız; Postgres'te `AspNetUsers` tablosunda `admin@test.local` kaydı var
(`psql` veya pgAdmin ile kontrol). Config boşken (`user-secrets remove`) tekrar dene: kullanıcı
oluşturulmaz, açılış yine hatasız.

- [ ] **Step 6: Commit**

```bash
git add src/others/Identity.Server/Options/BootstrapAdmin.cs \
        src/others/Identity.Server/Connect/SeedHostedService.cs \
        src/others/Identity.Server/Program.cs \
        src/others/Identity.Server/appsettings.Development.json
git commit -m "feat(042): bootstrap admin idempotent seed"
```

---

### Task 2: ClientSeed model genişlemesi + external-admin-agent seed girdisi

**Files:**
- Modify: `src/others/Identity.Server/Config.cs`

**Interfaces:**
- Consumes: yok (saf veri modeli).
- Produces: `ClientSeed.IsPublic`, `ClientSeed.AllowAuthorizationCode`, `ClientSeed.AllowRefreshToken`,
  `ClientSeed.RedirectUris` — Task 3'ün `SeedHostedService.BuildDescriptor`'ı bunları okuyacak.
  `Config.ExternalAdminAgentClientId` (const), `Config.ClaudeCallbackRedirectUris` (`string[]`),
  `Config.IdentityScopes` (`string[]`) — Task 4 ve 5 bunları kullanacak.

- [ ] **Step 1: `ClientSeed` sınıfını genişlet**

`Config.cs`'in en altındaki `ClientSeed` sınıfını şu hale getir (mevcut alanlar KORUNUR, `required`
kaldırılmaz — yalnız yeni alanlar eklenir ve `ClientSecret` nullable olur):

```csharp
public sealed class ClientSeed
{
    public required string ClientId { get; init; }
    // Public (PKCE) istemcide secret YOK — null bırakılır. Confidential (mevcut M2M) istemciler
    // ClientSecret'ı zorunlu tutmaya devam eder (RequireSecret helper'ı çağıran taraf sağlar).
    public string? ClientSecret { get; init; }
    public required string DisplayName { get; init; }
    // Public istemci = secret'sız + PKCE zorunlu (BuildDescriptor Requirements ekler).
    public bool IsPublic { get; init; }
    public bool AllowAuthorizationCode { get; init; }
    public bool AllowRefreshToken { get; init; }
    public string[] RedirectUris { get; init; } = [];
    public string[] Scopes { get; init; } = [];
}
```

- [ ] **Step 2: `external-admin-agent` client id sabiti + Claude callback listesi ekle**

`Config` sınıfının en üstüne (`ScopeResources` tanımından ÖNCE) ekle:

```csharp
    // G3: seed'li admin istemcisi (Claude Desktop). Loopback muafiyeti Task 3'teki
    // AdminAgentApplicationManager ile yalnız BU ClientId için.
    public const string ExternalAdminAgentClientId = "external-admin-agent";

    // Claude sabit callback'leri.
    public static readonly string[] ClaudeCallbackRedirectUris =
    [
        "https://claude.ai/api/mcp/auth_callback",
        "https://claude.com/api/mcp/auth_callback",
    ];

    // OIDC identity scope'ları — API scope'larından AYRI, RegisterScopes'a birlikte verilir.
    public static readonly string[] IdentityScopes = ["openid", "profile"];
```

- [ ] **Step 3: `Clients` listesine `external-admin-agent` girdisini ekle**

Mevcut `Clients(IConfiguration configuration)` listesinin SONUNA (son `ecommerce-onboarding`
girdisinden sonra) ekle:

```csharp
        // G3: seed'li admin istemcisi — Claude Desktop, public+PKCE, secret'sız. Consent YOK
        // (seed istemci → Implicit, bkz. SeedHostedService.BuildDescriptor).
        new ClientSeed
        {
            ClientId = ExternalAdminAgentClientId,
            ClientSecret = null,
            DisplayName = "External admin agent (Claude Desktop)",
            IsPublic = true,
            AllowAuthorizationCode = true,
            AllowRefreshToken = true,
            RedirectUris = ClaudeCallbackRedirectUris,
            Scopes = ["openid", "profile", "merchant.read", "merchant.write", "commission.read", "commission.write"],
        },
```

- [ ] **Step 4: Build doğrula (henüz BuildDescriptor güncellenmedi — derleme hatasız olmalı, yeni alanlar kullanılmıyor olsa da opsiyonel)**

```bash
dotnet build src/others/Identity.Server/Identity.Server.csproj
```

Beklenen: 0 hata (yeni `ClientSeed` alanları opsiyonel/default'lu; mevcut 5 client girdisi hâlâ
geçerli çünkü `ClientSecret` artık nullable ama onlar zaten değer veriyor).

- [ ] **Step 5: Commit**

```bash
git add src/others/Identity.Server/Config.cs
git commit -m "feat(042): ClientSeed public+PKCE alanları + external-admin-agent seed girdisi"
```

---

### Task 3: BuildDescriptor güncellemesi + loopback muafiyeti (AdminAgentApplicationManager)

**Files:**
- Modify: `src/others/Identity.Server/Connect/SeedHostedService.cs`
- Create: `src/others/Identity.Server/Connect/AdminAgentApplicationManager.cs`
- Modify: `src/others/Identity.Server/Program.cs` (AddCore içine `ReplaceApplicationManager`)

**Interfaces:**
- Consumes: `Config.ExternalAdminAgentClientId` (Task 2).
- Produces: `AdminAgentApplicationManager<TApplication>` — Program.cs'te `AddCore` tarafından
  tüketilir.

- [ ] **Step 1: `SeedHostedService.cs`'teki istemci-seed döngüsünün `BuildDescriptor` çağrısını bul**

Mevcut kod (referans, DEĞİŞMEYECEK method imzası):

```csharp
        foreach (var client in Config.Clients(configuration))
        {
            var descriptor = BuildDescriptor(client);
            ...
        }
```

`BuildDescriptor` metodu henüz PG'de private static olarak TANIMLI DEĞİL (bugün seed döngüsü
muhtemelen doğrudan `OpenIddictApplicationDescriptor` inline kuruyor) — mevcut inline kurulum
kodunu OKU, aşağıdaki private metotla DEĞİŞTİR.

- [ ] **Step 2: `BuildDescriptor` private metodunu ekle/güncelle**

`SeedHostedService.cs` içine (sınıfın içine, `StartAsync`'ten sonra) ekle:

```csharp
    private static OpenIddictApplicationDescriptor BuildDescriptor(ClientSeed client)
    {
        var d = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = client.IsPublic ? null : client.ClientSecret,
            DisplayName = client.DisplayName,
            ClientType = client.IsPublic ? ClientTypes.Public : ClientTypes.Confidential,
            // Seed istemciler Implicit (consent yok) — DCR henüz yok, hepsi ilk-taraf.
            ConsentType = ConsentTypes.Implicit,
        };

        if (client.AllowAuthorizationCode)
        {
            d.Permissions.Add(Permissions.Endpoints.Authorization);
            d.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            d.Permissions.Add(Permissions.ResponseTypes.Code);
            d.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }

        // Mevcut 5 M2M istemci AllowAuthorizationCode=false ile gelir → hepsi client_credentials
        // permission'ı alır (REGRESYON YOK). external-admin-agent AllowAuthorizationCode=true
        // olduğundan bu dala GİRMEZ — public istemciye client_credentials permission'ı eklenmez.
        if (!client.AllowAuthorizationCode)
            d.Permissions.Add(Permissions.GrantTypes.ClientCredentials);

        if (client.AllowRefreshToken)
            d.Permissions.Add(Permissions.GrantTypes.RefreshToken);

        d.Permissions.Add(Permissions.Endpoints.Token);

        foreach (var uri in client.RedirectUris)
            d.RedirectUris.Add(new Uri(uri));

        foreach (var s in client.Scopes)
            d.Permissions.Add(Permissions.Prefixes.Scope + s);

        return d;
    }
```

- [ ] **Step 3: Seed döngüsünü `BuildDescriptor` kullanacak şekilde güncelle (henüz değilse)**

```csharp
        foreach (var client in Config.Clients(configuration))
        {
            var descriptor = BuildDescriptor(client);
            var existing = await apps.FindByClientIdAsync(client.ClientId, ct);
            if (existing is null)
                await apps.CreateAsync(descriptor, ct);
            else
                await apps.UpdateAsync(existing, descriptor, ct);
        }
```

(Mevcut kodda zaten bu şekle yakınsa yalnız `BuildDescriptor` çağrısını ekle; birebir inline
`OpenIddictApplicationDescriptor` kurulumu varsa yukarıdaki metotla değiştir.)

- [ ] **Step 4: `AdminAgentApplicationManager.cs` dosyasını oluştur**

```csharp
using Microsoft.Extensions.Options;
using OpenIddict.Core;

namespace Identity.Server.Connect;

// G3: YALNIZ seed'li yönetim istemcisi (external-admin-agent) için RFC 8252 §7.3 loopback
// redirect muafiyeti — MCP Inspector/CLI istemcileri dinamik portlu http://localhost|127.0.0.1
// callback'i kullanır, seed'e port yazılamaz. Diğer TÜM istemciler birebir eşleşme kuralında kalır.
public sealed class AdminAgentApplicationManager<TApplication>(
    IOpenIddictApplicationCache<TApplication> cache,
    ILogger<OpenIddictApplicationManager<TApplication>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> options,
    IOpenIddictApplicationStore<TApplication> store)
    : OpenIddictApplicationManager<TApplication>(cache, logger, options, store)
    where TApplication : class
{
    public override async ValueTask<bool> ValidateRedirectUriAsync(
        TApplication application, string uri, CancellationToken cancellationToken = default)
    {
        if (await base.ValidateRedirectUriAsync(application, uri, cancellationToken))
            return true;

        var clientId = await GetClientIdAsync(application, cancellationToken);
        if (clientId != Config.ExternalAdminAgentClientId)
            return false;

        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
               && parsed.Scheme == Uri.UriSchemeHttp
               && parsed.Host is "localhost" or "127.0.0.1";
    }
}
```

- [ ] **Step 5: `Program.cs`'in `AddCore` bloğuna `ReplaceApplicationManager` ekle**

Mevcut:
```csharp
    .AddCore(options =>
        options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>())
```

Şuna değiştir:
```csharp
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>();
        // G3: seed admin istemcisine loopback redirect muafiyeti (yalnız o ClientId).
        options.ReplaceApplicationManager(typeof(AdminAgentApplicationManager<>));
    })
```

- [ ] **Step 6: Build + canlı seed doğrula**

```bash
dotnet build src/others/Identity.Server/Identity.Server.csproj
dotnet run --project src/others/Identity.Server/Identity.Server.csproj &
sleep 5
# OpenIddictApplications tablosunda external-admin-agent kaydını doğrula:
psql "postgresql://postgres:postgres@localhost:5432/identityDb" -c \
  "select \"ClientId\", \"ClientType\", \"ConsentType\" from \"OpenIddictApplications\" where \"ClientId\"='external-admin-agent';"
```

Beklenen: bir satır, `ClientType=public`, `ConsentType=implicit`. Mevcut 5 M2M istemcinin hâlâ
`confidential` olduğunu da kontrol et (regresyon guard'ı).

- [ ] **Step 7: Commit**

```bash
git add src/others/Identity.Server/Connect/SeedHostedService.cs \
        src/others/Identity.Server/Connect/AdminAgentApplicationManager.cs \
        src/others/Identity.Server/Program.cs
git commit -m "feat(042): BuildDescriptor public+PKCE desteği + loopback muafiyeti"
```

---

### Task 4: OpenIddict server — authorization_code flow açılışı + resource parametresi yoksayma

**Files:**
- Create: `src/others/Identity.Server/Connect/IgnoreResourceParameterHandler.cs`
- Modify: `src/others/Identity.Server/Program.cs`
- Modify: `src/others/Identity.Server/Connect/ScopeClaimArrayHandler.cs` (OidcClaimDestinations)

**Interfaces:**
- Consumes: yok (OpenIddict server pipeline event handler'ları).
- Produces: `/connect/authorize` ucu (Task 5 map eder), id_token destinasyonu (Task 6 kullanır).

- [ ] **Step 1: `IgnoreResourceParameterHandler.cs` oluştur**

```csharp
using OpenIddict.Server;

namespace Identity.Server.Connect;

// G3: MCP istemcileri (Claude Desktop / mcp-remote) RFC 8707 `resource` parametresiyle MCP URL'i
// gönderir. OpenIddict bu değeri scope'lara bağlı resource'ların (merchant.api gibi mantıksal
// audience adları) alt kümesi olarak doğrular → invalid_target. Audience zaten scope→resource
// eşlemesinden üretildiği için `resource` parametresi YOK SAYILIR.
public static class IgnoreResourceParameterHandler
{
    public sealed class ForAuthorization
        : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>
    {
        public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
            OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateAuthorizationRequestContext>()
                .UseSingletonHandler<ForAuthorization>()
                .SetOrder(int.MinValue + 100_000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build();

        public ValueTask HandleAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Request.Resources = null;
            return default;
        }
    }

    public sealed class ForToken
        : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
    {
        public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
            OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
                .UseSingletonHandler<ForToken>()
                .SetOrder(int.MinValue + 100_000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build();

        public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Request.Resources = null;
            return default;
        }
    }
}
```

- [ ] **Step 2: `Program.cs`'teki `AddServer` bloğunu güncelle**

Mevcut:
```csharp
        options.SetTokenEndpointUris("connect/token");
        options.AllowClientCredentialsFlow();

        options.RegisterScopes([.. Config.AllApiScopes]);
```

Şuna değiştir:
```csharp
        options.SetAuthorizationEndpointUris("connect/authorize")
               .SetTokenEndpointUris("connect/token");

        options.AllowClientCredentialsFlow()
               .AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();

        options.RegisterScopes([.. Config.AllApiScopes, .. Config.IdentityScopes]);
```

Ve aynı blokta, `options.AddEventHandler(ScopeClaimArrayHandler.Descriptor);` satırından SONRA
ekle:

```csharp
        // G3: MCP `resource` parametresi yok sayılır (yukarı bkz).
        options.AddEventHandler(IgnoreResourceParameterHandler.ForAuthorization.Descriptor);
        options.AddEventHandler(IgnoreResourceParameterHandler.ForToken.Descriptor);
```

Ve en alttaki `UseAspNetCore()` bloğunu güncelle:
```csharp
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough();
```

- [ ] **Step 3: `ConfigureApplicationCookie` ekle (login sayfası yolu için)**

`Program.cs`'te `builder.Services.AddIdentity<ApplicationUser, IdentityRole>()...` bloğundan HEMEN
SONRA ekle:

```csharp
// G3: login yolu — cookie doğrulaması başarısızsa buraya yönlenir (Task 5'teki AuthorizeEndpoint
// bu davranışa güvenir: Results.Challenge → varsayılan LoginPath).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
});
```

- [ ] **Step 4: `OidcClaimDestinations`'ı güncelle (id_token desteği)**

`ScopeClaimArrayHandler.cs`'in en altındaki (M2M-only) sınıfı DEĞİŞTİR:

Eski:
```csharp
public static class OidcClaimDestinations
{
    public static IEnumerable<string> GetDestinations(Claim claim) => [Destinations.AccessToken];
}
```

Yeni:
```csharp
// access_token + (insan akışında) id_token claim destinasyonları. M2M istemcilerde id_token
// hiç üretilmediğinden bu ayrım onlara etki etmez.
public static class OidcClaimDestinations
{
    public static IEnumerable<string> GetDestinations(Claim claim) => claim.Type switch
    {
        Claims.Subject or Claims.Name or Claims.Email =>
            [Destinations.AccessToken, Destinations.IdentityToken],
        _ => [Destinations.AccessToken],
    };
}
```

- [ ] **Step 5: Build doğrula**

```bash
dotnet build src/others/Identity.Server/Identity.Server.csproj
```

Beklenen: 0 hata. (Task 5 tamamlanana kadar `/connect/authorize`'a istek 404 döner — endpoint
henüz map edilmedi, bu NORMAL.)

- [ ] **Step 6: Commit**

```bash
git add src/others/Identity.Server/Connect/IgnoreResourceParameterHandler.cs \
        src/others/Identity.Server/Connect/ScopeClaimArrayHandler.cs \
        src/others/Identity.Server/Program.cs
git commit -m "feat(042): authorization_code flow + resource parametresi yoksayma"
```

---

### Task 5: AuthorizeEndpoint (insan etkileşim ucu)

**Files:**
- Create: `src/others/Identity.Server/Connect/AuthorizeEndpoint.cs`
- Modify: `src/others/Identity.Server/Program.cs` (map + `UseAuthentication`/`UseAuthorization`)

**Interfaces:**
- Consumes: `Config.IdentityScopes` (Task 2), `OidcClaimDestinations.GetDestinations` (Task 4).
- Produces: `/connect/authorize` GET+POST ucu.

- [ ] **Step 1: `AuthorizeEndpoint.cs` oluştur**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Server.Connect;

// /connect/authorize — code+PKCE akışının kullanıcı etkileşim ucu. Tek admin, seed istemci
// (Implicit consent) — consent dalı YOK (YAGNI; DCR/çoklu-kullanıcı gelirse ayrı spec ekler).
public static class AuthorizeEndpoint
{
    public static void MapAuthorizeEndpoint(this WebApplication app) =>
        app.MapMethods("/connect/authorize", ["GET", "POST"], HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IOpenIddictScopeManager scopeManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OIDC authorize isteği çözülemedi.");

        // Cookie ile kimlik doğrula. Yoksa login'e yönlendir (returnUrl bu isteğin kendisi).
        var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (result is not { Succeeded: true })
        {
            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                [IdentityConstants.ApplicationScheme]);
        }

        var user = await userManager.GetUserAsync(result.Principal!)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user));
        identity.SetClaim(Claims.Name, await userManager.GetUserNameAsync(user));
        identity.SetClaim(Claims.Email, await userManager.GetEmailAsync(user));

        // Tek admin, tek istemci — requested scope'lar OpenIddict tarafından zaten istemcinin
        // kayıtlı Scopes listesine göre süzülmüştür (built-in validation); ek filtre gerekmez.
        identity.SetScopes(request.GetScopes());

        var resources = new List<string>();
        await foreach (var resource in scopeManager.ListResourcesAsync(identity.GetScopes()))
            resources.Add(resource);
        identity.SetResources(resources);

        identity.SetDestinations(OidcClaimDestinations.GetDestinations);

        return Results.SignIn(new ClaimsPrincipal(identity), null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
```

- [ ] **Step 2: `Program.cs`'e map + auth middleware ekle**

`app.UseRouting();` satırından SONRA (henüz yoksa) ekle:
```csharp
app.UseAuthentication();
app.UseAuthorization();
```

`app.MapTokenEndpoint();` satırının ÜSTÜNE ekle:
```csharp
app.MapAuthorizeEndpoint();
```

- [ ] **Step 3: Build doğrula**

```bash
dotnet build src/others/Identity.Server/Identity.Server.csproj
```

- [ ] **Step 4: Commit**

```bash
git add src/others/Identity.Server/Connect/AuthorizeEndpoint.cs src/others/Identity.Server/Program.cs
git commit -m "feat(042): /connect/authorize ucu"
```

---

### Task 6: TokenEndpoint dallanması (authorization_code + refresh_token)

**Files:**
- Modify: `src/others/Identity.Server/Connect/TokenEndpoint.cs`

**Interfaces:**
- Consumes: `OidcClaimDestinations.GetDestinations` (Task 4).
- Produces: `/connect/token` artık 3 grant type'ı destekler (mevcut client_credentials dalı
  BİREBİR KORUNUR).

- [ ] **Step 1: `TokenEndpoint.cs`'i güncelle — mevcut client_credentials bloğunu KORU, yeni dal ekle**

Mevcut `HandleAsync` gövdesinin SONUNA (`if (!request.IsClientCredentialsGrantType()) throw ...`
satırını SİLEREK, onun yerine) ekle:

```csharp
    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IOpenIddictScopeManager scopeManager,
        IOpenIddictApplicationManager applicationManager,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OIDC token isteği çözülemedi.");

        if (request.IsClientCredentialsGrantType())
        {
            // M2M: sub = client id (029 paritesi). --- MEVCUT KOD DEĞİŞMEDEN BURADA KALIR ---
            var identity = new ClaimsIdentity(
                TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

            identity.SetClaim(Claims.Subject, request.ClientId);

            var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
                ?? throw new InvalidOperationException("İstemci kaydı bulunamadı.");
            var properties = await applicationManager.GetPropertiesAsync(application);
            if (properties.TryGetValue(MerchantClientEventHandler.MerchantIdProperty, out var merchantId))
                identity.SetClaim(MerchantClientEventHandler.MerchantIdProperty, merchantId.GetString());

            identity.SetScopes(request.GetScopes());

            var resources = new List<string>();
            await foreach (var resource in scopeManager.ListResourcesAsync(identity.GetScopes()))
                resources.Add(resource);
            identity.SetResources(resources);

            identity.SetDestinations(OidcClaimDestinations.GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity), null,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // G3: insan akışları — OpenIddict'in code/refresh token'da sakladığı principal'ı geri al.
        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var authResult = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!authResult.Succeeded || authResult.Principal is not { } principal)
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Token süresi geçmiş veya iptal edilmiş.",
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            var user = await userManager.FindByIdAsync(principal.GetClaim(Claims.Subject)!);
            if (user is null || !await signInManager.CanSignInAsync(user))
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Kullanıcı artık giriş yapamıyor.",
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            var identity = new ClaimsIdentity(principal.Claims,
                TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
            identity.SetDestinations(OidcClaimDestinations.GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity), null,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("Desteklenmeyen grant type.");
    }
```

Dosya başındaki `using` bloğuna ekle (henüz yoksa):
```csharp
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Identity;
```

- [ ] **Step 2: Build doğrula**

```bash
dotnet build src/others/Identity.Server/Identity.Server.csproj
```

- [ ] **Step 3: Mevcut M2M istemcilerin regresyonsuz çalıştığını canlı doğrula**

```bash
dotnet run --project src/others/Identity.Server/Identity.Server.csproj &
sleep 5
curl -sk -X POST https://localhost:5101/connect/token \
  -d "grant_type=client_credentials" -d "client_id=admin-ui" \
  -d "client_secret=<user-secrets'taki admin-ui secret>" \
  -d "scope=merchant.read merchant.write" -w "\nHTTP %{http_code}\n"
```

Beklenen: `HTTP 200` + `access_token` alanı dolu (Task 6 öncesiyle BİREBİR AYNI davranış).

- [ ] **Step 4: Commit**

```bash
git add src/others/Identity.Server/Connect/TokenEndpoint.cs
git commit -m "feat(042): /connect/token authorization_code+refresh_token dalı (client_credentials korunur)"
```

---

### Task 7: Login sayfası (Razor Pages)

**Files:**
- Create: `src/others/Identity.Server/Pages/Account/Login.cshtml`
- Create: `src/others/Identity.Server/Pages/Account/Login.cshtml.cs`

**Interfaces:**
- Consumes: `SignInManager<ApplicationUser>` (ASP.NET Identity, zaten kurulu).
- Produces: `/Account/Login?ReturnUrl=...` sayfası — `AuthorizeEndpoint`'in `Results.Challenge`'ı
  buraya yönlenir (`ConfigureApplicationCookie` Task 4'te bu yolu ayarladı).

- [ ] **Step 1: `Login.cshtml.cs` PageModel'i yaz**

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Identity.Server.Pages.Account;

// G3: minimal login — kayıt/şifre sıfırlama/2FA YOK (tek bootstrap admin, YAGNI).
public class LoginModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; } = "/";

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, isPersistent: true, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            ErrorMessage = "E-posta veya parola hatalı.";
            return Page();
        }

        return LocalRedirect(ReturnUrl);
    }
}
```

- [ ] **Step 2: `Login.cshtml` view'ını yaz**

```cshtml
@page
@model Identity.Server.Pages.Account.LoginModel
<!DOCTYPE html>
<html lang="tr">
<head><meta charset="utf-8"><title>Admin Login</title></head>
<body>
    <h1>PaymentGateway Admin Login</h1>
    @if (Model.ErrorMessage is not null)
    {
        <p style="color:red">@Model.ErrorMessage</p>
    }
    <form method="post">
        <div>
            <label>E-posta</label>
            <input asp-for="Input.Email" />
        </div>
        <div>
            <label>Parola</label>
            <input asp-for="Input.Password" type="password" />
        </div>
        <button type="submit">Giriş</button>
    </form>
</body>
</html>
```

> Not: harici CSS/JS YOK (YAGNI) — `UseStaticFiles()` gerektirmez, mevcut Program.cs'e dokunmaz.

- [ ] **Step 3: Build doğrula**

```bash
dotnet build src/others/Identity.Server/Identity.Server.csproj
```

- [ ] **Step 4: Commit**

```bash
git add src/others/Identity.Server/Pages/Account/Login.cshtml src/others/Identity.Server/Pages/Account/Login.cshtml.cs
git commit -m "feat(042): minimal admin login sayfası"
```

---

### Task 8: Uçtan uca canlı doğrulama (quickstart)

**Files:**
- Create: `specs/042-admin-oauth-plane/quickstart.md`

**Interfaces:**
- Consumes: Task 1-7'nin tamamı (Aspire AppHost ile ayakta).

- [ ] **Step 1: `quickstart.md` yaz**

```markdown
# 042 Admin OAuth — Canlı Doğrulama

1. `dotnet user-secrets set "BootstrapAdmin:Email" "<email>" --project src/others/Identity.Server`
   `dotnet user-secrets set "BootstrapAdmin:Password" "<parola>" --project src/others/Identity.Server`
2. `dotnet run --project src/aspire/AppHost/AppHost.csproj` (tüm sistem).
3. `npx mcp-remote https://localhost:5202/mcp` gibi bir araçla (veya Claude Desktop config'inde
   `command: npx, args: [-y, mcp-remote, https://localhost:5202/mcp]`) Merchant.Api'nin MEVCUT
   `/mcp`'sine bağlanmayı dene (henüz `/mcp-admin` yok — bu yalnız OAuth zincirini test eder).
4. Tarayıcı `https://localhost:5101/Account/Login`'e yönlenmeli.
5. Bootstrap admin email+parola ile giriş yap.
6. mcp-remote/Claude Desktop token alır (consent ekranı GÖRÜNMEZ — beklenen).
7. Token'ı decode et (jwt.io veya `curl` ile `/connect/userinfo` YOK — bu spec'te yok; token
   payload'ını `base64 -d` ile aç): `merchant_id` claim'i OLMAMALI.
8. Regresyon: `curl -X POST https://localhost:5101/connect/token -d grant_type=client_credentials
   -d client_id=admin-ui -d client_secret=<secret> -d scope=merchant.read` HÂLÂ 200 dönmeli.

**Beklenen sonuç**: 6-7-8 hepsi geçerse SC-001/SC-002/SC-004 doğrulanmış olur.
```

- [ ] **Step 2: Adım adım gerçekten çalıştır, sonucu bu dosyaya (bir "Sonuç" bölümü ekleyerek) not et.**

- [ ] **Step 3: Commit**

```bash
git add specs/042-admin-oauth-plane/quickstart.md
git commit -m "docs(042): canlı doğrulama quickstart + sonuç"
```

## Self-Review Notları (yazarken yapıldı)

- **Spec kapsama:** FR-001..FR-009 → Task 1 (FR-002/FR-003 kısmen), Task 2-3 (FR-005/FR-006/FR-009),
  Task 4-6 (FR-001/FR-007/FR-008 temeli), Task 5-7 (FR-003), Task 8 (SC-001..004 doğrulama).
  FR-008 (PRM extension'ın Common'a taşınması) BU PLANDA YOK — ayrı, küçük bir task olarak
  Merchant.Api `/mcp-admin` planına (#2) bırakıldı (o zaman gerçekten tüketilecek, YAGNI).
- **Placeholder taraması:** yok — her adımda gerçek kod/komut var.
- **Tip tutarlılığı:** `ClientSeed`, `Config.ExternalAdminAgentClientId`, `OidcClaimDestinations`,
  `AdminAgentApplicationManager<TApplication>` isimleri Task 2→3→4→5 boyunca birebir tutarlı.
