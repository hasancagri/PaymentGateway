namespace Identity.Server;

// Seed sabitleri: scope registry + istemci listesi. SeedHostedService açılışta OpenIddict
// application/scope manager'larına idempotent yazar (yalnız BU statik liste — G2'nin çalışma
// anında ekleyeceği merchant client'larına dokunulmaz).
public static class Config
{
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

    // Scope → audience (resource) haritası. Token üretiminde ListResourcesAsync bu eşlemeden
    // 'aud' claim'ini üretir; servisler kendi adını (merchant.api...) ValidateAudience ile arar.
    // G2/G5 genişlemesi (cards.write, charge) buraya eklenir.
    public static readonly IReadOnlyDictionary<string, string> ScopeResources =
        new Dictionary<string, string>
        {
            ["payment.read"] = "payment.api",
            ["payment.write"] = "payment.api",
            // 017: kart vault capability scope — Payment.Api audience'ı (Active merchant demetine verilir).
            ["cards.write"] = "payment.api",
            // 033: ödeme çekim capability scope — Payment.Api audience'ı (yalnız Active merchant).
            ["payment.charge"] = "payment.api",
            ["merchant.read"] = "merchant.api",
            ["merchant.write"] = "merchant.api",
            // 043: admin-tool capability scope — Merchant.Api Wolverine ScopeAuthorizationMiddleware'i tool-bazlı uygular.
            ["merchant.admin"] = "merchant.api",
            ["commission.read"] = "commission.api",
            ["commission.write"] = "commission.api",
        };

    public static IEnumerable<string> AllApiScopes => ScopeResources.Keys;

    // İstemci kayıtları. Secret'lar koda GÖMÜLMEZ (FR-011): Clients:<id>:Secret anahtarından okunur
    // (appsettings dev varsayılanı + user-secrets/env override); store hash'leyerek saklar.
    public static IReadOnlyList<ClientSeed> Clients(IConfiguration configuration) =>
    [
        // Admin BFF m2m: tüm yönetim ekranları (admin-düzlemi token).
        new ClientSeed
        {
            ClientId = "admin-ui",
            ClientSecret = RequireSecret(configuration, "admin-ui"),
            DisplayName = "Admin BFF (m2m)",
            Scopes =
            [
                "merchant.read", "merchant.write", "merchant.admin",
                "commission.read", "commission.write",
                "payment.read", "payment.write",
            ],
        },
        // Payment.Agent m2m: MCP tool çağrıları (yüzey tek policy: payment.write).
        new ClientSeed
        {
            ClientId = "payment-agent",
            ClientSecret = RequireSecret(configuration, "payment-agent"),
            DisplayName = "Payment agent (m2m)",
            Scopes = ["payment.read", "payment.write"],
        },
        // 013: Merchant.Agent m2m — başvuru MCP tool'ları (Merchant.Api /mcp, merchant.write).
        // 019: komisyon teklif/pazarlık MCP tool'ları (Commission.Api /mcp, commission.write) eklendi.
        new ClientSeed
        {
            ClientId = "merchant-agent",
            ClientSecret = RequireSecret(configuration, "merchant-agent"),
            DisplayName = "Merchant agent (m2m)",
            Scopes = ["merchant.read", "merchant.write", "commission.write"],
        },
        // 013: Identity aktivasyon sayfası → Merchant.Api redeem (sanksiyonlu senkron çağrı).
        // Claim'siz (AdminPlaneOnly geçer); merchant.write ile bileti kullanır.
        new ClientSeed
        {
            ClientId = "identity-activation",
            ClientSecret = RequireSecret(configuration, "identity-activation"),
            DisplayName = "Identity activation page (m2m)",
            Scopes = ["merchant.write"],
        },
        // 013 E1: harici aday site (ECommerce) → Merchant.Api /mcp submit_registration otomatik sürüşü.
        // Claim'siz makine token'ı; merchant.write ile başvuru yapar.
        new ClientSeed
        {
            ClientId = "ecommerce-onboarding",
            ClientSecret = RequireSecret(configuration, "ecommerce-onboarding"),
            DisplayName = "ECommerce onboarding client (m2m)",
            Scopes = ["merchant.read", "merchant.write"],
        },
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
            Scopes = ["openid", "profile", "merchant.read", "merchant.write", "merchant.admin", "commission.read", "commission.write"],
        },
    ];

    private static string RequireSecret(IConfiguration configuration, string clientId) =>
        configuration[$"Clients:{clientId}:Secret"]
        ?? throw new InvalidOperationException($"Clients:{clientId}:Secret yapılandırılmamış.");
}

// Tek istemci seed tanımı. 011'de tek grant: client_credentials (insan akışı yok — D1).
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