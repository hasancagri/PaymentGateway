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
            // 017: kart vault capability scope — Payment.Api audience'ı (Active merchant demetine verilir).
            ["cards.write"] = "payment.api",
            ["merchant.read"] = "merchant.api",
            ["merchant.write"] = "merchant.api",
            ["commission.read"] = "commission.api",
            ["commission.write"] = "commission.api",
            // 013: altyapı MCP yüzeyi (Excel.Mcp). 016: Mail.Mcp kaldırıldı (Mail.Worker = RabbitMQ, MCP değil).
            ["document.generate"] = "excel.mcp",
        };

    public static IEnumerable<string> AllApiScopes => ScopeResources.Keys;

    // İstemci kayıtları. Secret'lar koda GÖMÜLMEZ (FR-011): Clients:<id>:Secret anahtarından okunur
    // (appsettings dev varsayılanı + user-secrets/env override); store hash'leyerek saklar.
    public static IReadOnlyList<ClientSeed> Clients(IConfiguration configuration) =>
    [
        // Admin BFF m2m: tüm yönetim ekranları + 013 komisyon Excel orkestrasyonu (harici LLM/MCP
        // client admin-düzlemi token). document.generate (Excel.Mcp) eklendi (merchant_id claim'siz).
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
                "document.generate",
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