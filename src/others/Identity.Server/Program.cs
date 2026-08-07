using Identity.Server;
using Identity.Server.Connect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Aspire çalışma anında enjekte eder; design-time (migration üretimi) için fallback.
var connectionString = builder.Configuration.GetConnectionString("identityDb")
                       ?? "Host=localhost;Port=5432;Database=identityDb;Username=postgres;Password=postgres";

var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
    // OpenIddict EF Core store'ları aynı context'i kullanır.
    options.UseOpenIddict();
});

// Kullanıcı deposu şimdiden kurulur (kullanıcı seed edilmez) — G3/RBAC zemini + tek Initial migration (D2).
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddOpenIddict()
    .AddCore(options =>
        options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>())
    .AddServer(options =>
    {
        // Sabit issuer — tüm servislerin IdentityOption:Address değeriyle birebir (D6).
        // 5001 ECommerce Identity'de; A2A senaryosunda iki sistem aynı anda koşar.
        options.SetIssuer(new Uri("https://localhost:5101"));

        // Yalnız token ucu + client_credentials (insan akışı yok — D1).
        options.SetTokenEndpointUris("connect/token");
        options.AllowClientCredentialsFlow();

        options.RegisterScopes([.. Config.AllApiScopes]);

        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // Access token düz imzalı JWT olsun ki servislerin JwtBearer'ı çözebilsin.
        options.DisableAccessTokenEncryption();

        // R3: access token scope claim'ini JSON dizisine çevir (029 tuzağı — D3).
        options.AddEventHandler(ScopeClaimArrayHandler.Descriptor);

        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough();
    });

// Açılışta idempotent client + scope seed.
builder.Services.AddHostedService<SeedHostedService>();

var app = builder.Build();

// Açılışta migration'ları uygula (dev kolaylığı; Postgres Aspire ile hazır olur).
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}

app.UseRouting();

// Tek uç: /connect/token (OpenIddict passthrough ile ASP.NET Core'da işlenir).
app.MapTokenEndpoint();

app.Run();