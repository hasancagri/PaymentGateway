namespace Payment.Identity;

// Seed sabitleri: scope registry + istemci listesi. SeedHostedService açılışta OpenIddict
// application/scope manager'larına idempotent yazar (yalnız BU statik liste — G2'nin çalışma
// anında ekleyeceği merchant client'larına dokunulmaz).
public static class Config
{
    // OIDC identity scope'ları — API scope'larından AYRI, RegisterScopes'a birlikte verilir.
    public static readonly string[] IdentityScopes = ["openid", "profile"];

    // 044/047 R5: seed listesinden çıkarılan ölü client'lar — SeedHostedService açılışta store'dan
    // SİLER (yalnız create/update'li seed store kaydını bırakır, ölü kimlik token almaya devam
    // ederdi — fail-closed). 047: external-admin-agent MCP kimlik yüzeyi AgentPlatform IdP'ye taşındı.
    // 048: admin-ui SÖKÜLDÜ — Razor Admin BFF projesi kaldırıldı (hassas-veri kanalı hosted link'e
    // taşındı); ölü ama yetkili (merchant.admin + payment.write...) m2m kimlik yüzeyini bırakmamak için prune.
    public static readonly string[] RetiredClientIds = ["payment-agent", "merchant-agent", "external-admin-agent", "admin-ui"];

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
        // 048: admin-ui seed SÖKÜLDÜ — Razor Admin BFF kaldırıldı (RetiredClientIds ile store'dan
        // prune edilir; ölü ama yetkili m2m kimlik yüzeyi bırakmamak için, fail-closed).
        // 044: payment-agent + merchant-agent seed'leri SİLİNDİ — A2A host'ları 038/043'te söküldü,
        // ölü ama yetkili (payment.write/commission.write) kimlik yüzeyi bırakıyorlardı (R5, fail-closed).
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
        // 047: external-admin-agent seed'i SÖKÜLDÜ — MCP kimlik yüzeyi AgentPlatform IdP'ye taşındı
        // (yüzey başına tek otorite; RetiredClientIds ile store'dan prune edilir).
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