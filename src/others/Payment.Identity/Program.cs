using Payment.Identity;
using Payment.Identity.Connect;
using Payment.Identity.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared;
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

// Kullanıcı deposu (Identity EF şeması) kurulur — RBAC/schema zemini. 047+048: insan login akışı
// SÖKÜLDÜ (external-admin-agent AgentPlatform IdP'ye taşındı); kullanıcı seed edilmez, cookie login yok.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

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

        options.SetTokenEndpointUris("connect/token");

        // 047+048: yalnız M2M (client_credentials). İnsan akışları (authorization_code/refresh) SÖKÜLDÜ.
        options.AllowClientCredentialsFlow();

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

        // MCP `resource` parametresi token isteğinde yok sayılır.
        options.AddEventHandler(IgnoreResourceParameterHandler.ForToken.Descriptor);

        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough();
    });

// Açılışta idempotent client + scope seed.
builder.Services.AddHostedService<SeedHostedService>();

// 013: aktivasyon Razor sayfası + Merchant.Api redeem istemcisi (service discovery: merchant-api).
builder.Services.AddRazorPages();
// Merchant.Api'ye doğrudan (sabit port 5202) — service discovery DNS'ine bağlı kalma (aktivasyon
// sayfası tek senkron redeem çağrısı). Config'ten override edilebilir.
builder.Services.AddHttpClient<Payment.Identity.Activation.MerchantActivationClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["MerchantApi:BaseUrl"] ?? "http://localhost:5202"));

// Aktivasyon istemcisi için Identity adresi POCO (runtime doğrudan IConfiguration okuması yasak; CLAUDE.md).
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

// 012: merchant.lifecycle fanout tüketimi — Merchant BC olayları OpenIddict istemci kaydına
// izdüşürülür (MerchantClientEventHandler). Message store YOK (D1): durable inbox kullanılamaz;
// kuyruk RabbitMQ tarafında durable, handler idempotent → inline işleme (ack handler bitince) yeterli.
builder.Host.UseWolverine(opts =>
{
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.MerchantLifecycle.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    rabbit.DeclareQueue(RabbitMqConstants.MerchantLifecycle.IdentityQueue);
    rabbit.BindExchange(RabbitMqConstants.MerchantLifecycle.Exchange)
        .ToQueue(RabbitMqConstants.MerchantLifecycle.IdentityQueue);

    opts.ListenToRabbitQueue(RabbitMqConstants.MerchantLifecycle.IdentityQueue).ProcessInline();

    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    // Handler parametreleri (IOpenIddictApplicationManager, ILogger) scoped container'dan
    // service-location ile çözülür; Wolverine 6 default'u bunu yasaklıyor — bilinçli izin (warn'lı).
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

// Tek uç: /connect/token (OpenIddict passthrough ile ASP.NET Core'da işlenir; yalnız M2M).
app.MapTokenEndpoint();

// 013: aktivasyon sayfası (/activation).
app.MapRazorPages();

app.Run();