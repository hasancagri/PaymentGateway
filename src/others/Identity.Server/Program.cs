using Identity.Server;
using Identity.Server.Connect;
using Identity.Server.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.RabbitMQ;

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

// G3: login yolu — cookie doğrulaması başarısızsa buraya yönlenir (Task 5'teki AuthorizeEndpoint
// bu davranışa güvenir: Results.Challenge → varsayılan LoginPath).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
});

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>();
        // 047: AdminAgentApplicationManager (loopback redirect muafiyeti) SÖKÜLDÜ — tek tüketicisi
        // external-admin-agent'tı, o da AgentPlatform IdP'ye taşındı. Kalan istemciler birebir eşleşme.
    })
    .AddServer(options =>
    {
        // Sabit issuer — tüm servislerin IdentityOption:Address değeriyle birebir (D6).
        // 5001 ECommerce Identity'de; A2A senaryosunda iki sistem aynı anda koşar.
        options.SetIssuer(new Uri("https://localhost:5101"));

        options.SetAuthorizationEndpointUris("connect/authorize")
               .SetTokenEndpointUris("connect/token");

        options.AllowClientCredentialsFlow()
               .AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();

        options.RegisterScopes([.. Config.AllApiScopes, .. Config.IdentityScopes]);

        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // Access token düz imzalı JWT olsun ki servislerin JwtBearer'ı çözebilsin.
        options.DisableAccessTokenEncryption();

        // 012: 15 dk global ömür — revocation kolu (self-contained JWT'de anlık iptal yok;
        // askıya alınan merchant en geç 15 dk'da düşer). Admin/Agent handler'ları proaktif yeniler.
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));

        // R3: access token scope claim'ini JSON dizisine çevir (029 tuzağı — D3).
        options.AddEventHandler(ScopeClaimArrayHandler.Descriptor);

        // G3: MCP `resource` parametresi yok sayılır (yukarı bkz).
        options.AddEventHandler(IgnoreResourceParameterHandler.ForAuthorization.Descriptor);
        options.AddEventHandler(IgnoreResourceParameterHandler.ForToken.Descriptor);

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough();
    });

// G3: bootstrap admin — email/parola boşsa seed atlanır (Options/BootstrapAdmin.cs).
builder.Services.AddOptions<Identity.Server.Options.BootstrapAdmin>()
    .BindConfiguration(nameof(Identity.Server.Options.BootstrapAdmin));
builder.Services.AddSingleton<Identity.Server.Options.BootstrapAdmin>(sp =>
    sp.GetRequiredService<IOptions<Identity.Server.Options.BootstrapAdmin>>().Value);

// Açılışta idempotent client + scope seed.
builder.Services.AddHostedService<SeedHostedService>();

// G3: insan login/authorize Razor sayfaları (/Account/Login).
builder.Services.AddRazorPages();

// Mesajlaşma altyapısı — 088 teardown: merchant.lifecycle tüketimi (Merchant BC) SÖKÜLDÜ. Wolverine
// kabuğu + rabbit bağlantısı, PaymentCompleted/PaymentFailed tüketicisi geldiğinde hazır durur.
builder.Host.UseWolverine(opts =>
{
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    // Handler parametreleri scoped container'dan service-location ile çözülür; Wolverine 6
    // default'u bunu yasaklıyor — bilinçli izin (warn'lı).
    opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;
});

var app = builder.Build();

// Açılışta migration'ları uygula (dev kolaylığı; Postgres Aspire ile hazır olur).
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// G3: /connect/authorize — insan etkileşim ucu (cookie login + PKCE code akışı).
app.MapAuthorizeEndpoint();

// Tek uç: /connect/token (OpenIddict passthrough ile ASP.NET Core'da işlenir).
app.MapTokenEndpoint();

// G3: insan login/authorize Razor sayfaları.
app.MapRazorPages();

app.Run();