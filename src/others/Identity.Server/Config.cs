namespace Identity.Server;

// Seed sabitleri: scope registry + istemci listesi. SeedHostedService açılışta OpenIddict
// application/scope manager'larına idempotent yazar (yalnız BU statik liste — G2'nin çalışma
// anında ekleyeceği merchant client'larına dokunulmaz).
public static class Config
{
    // Scope → audience (resource) haritası. Token üretiminde ListResourcesAsync bu eşlemeden
    // 'aud' claim'ini üretir; servisler kendi adını (merchant.api...) ValidateAudience ile arar.
    // G2/G5 genişlemesi (cards.write, charge) buraya eklenir.
    public static readonly IReadOnlyDictionary<string, string> ScopeResources =
        new Dictionary<string, string>
        {
            ["payment.read"] = "payment.api",
            ["payment.write"] = "payment.api",
            ["merchant.read"] = "merchant.api",
            ["merchant.write"] = "merchant.api",
            ["commission.read"] = "commission.api",
            ["commission.write"] = "commission.api",
        };

    public static IEnumerable<string> AllApiScopes => ScopeResources.Keys;

    // İstemci kayıtları. Secret'lar koda GÖMÜLMEZ (FR-011): Clients:<id>:Secret anahtarından okunur
    // (appsettings dev varsayılanı + user-secrets/env override); store hash'leyerek saklar.
    public static IReadOnlyList<ClientSeed> Clients(IConfiguration configuration) =>
    [
        // Admin BFF m2m: tüm yönetim ekranları (AdminTokenHandler 6 scope'la token alır).
        new ClientSeed
        {
            ClientId = "admin-ui",
            ClientSecret = RequireSecret(configuration, "admin-ui"),
            DisplayName = "Admin BFF (m2m)",
            Scopes =
            [
                "merchant.read", "merchant.write",
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
    ];

    private static string RequireSecret(IConfiguration configuration, string clientId) =>
        configuration[$"Clients:{clientId}:Secret"]
        ?? throw new InvalidOperationException($"Clients:{clientId}:Secret yapılandırılmamış.");
}

// Tek istemci seed tanımı. 011'de tek grant: client_credentials (insan akışı yok — D1).
public sealed class ClientSeed
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string DisplayName { get; init; }
    public string[] Scopes { get; init; } = [];
}