namespace Identity.Server;

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
    // 088 teardown: admin-ui (Admin BFF), identity-activation (aktivasyon sayfası), ecommerce-onboarding
    // (Merchant BC /mcp başvurusu) eklendi — Merchant/Commission BC + Admin söküldü, hepsi ölü kimlik.
    public static readonly string[] RetiredClientIds =
        ["payment-agent", "merchant-agent", "external-admin-agent", "admin-ui", "identity-activation", "ecommerce-onboarding"];

    // Scope → audience (resource) haritası. Token üretiminde ListResourcesAsync bu eşlemeden
    // 'aud' claim'ini üretir; servisler kendi adını (merchant.api...) ValidateAudience ile arar.
    // G2/G5 genişlemesi (cards.write, charge) buraya eklenir.
    public static readonly IReadOnlyDictionary<string, string> ScopeResources =
        new Dictionary<string, string>
        {
            ["payment.read"] = "payment.api",
            ["payment.write"] = "payment.api",
        };

    public static IEnumerable<string> AllApiScopes => ScopeResources.Keys;

    // İstemci kayıtları. Secret'lar koda GÖMÜLMEZ (FR-011): Clients:<id>:Secret anahtarından okunur
    // (appsettings dev varsayılanı + user-secrets/env override); store hash'leyerek saklar.
    // 088 teardown: statik seed istemci KALMADI. admin-ui/identity-activation/ecommerce-onboarding
    // Merchant/Commission BC + Admin BFF söküldüğü için öldü (RetiredClientIds ile store'dan prune).
    // Yeni Payment BC store→gateway yetkisi AgentPlatform IdP'de (ayrı otorite, anayasa İlke V).
    public static IReadOnlyList<ClientSeed> Clients(IConfiguration configuration) => [];
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