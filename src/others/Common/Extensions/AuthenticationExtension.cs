using Common.Options;
using Common.Utils.Authorization;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Common.Extensions;

public static class AuthenticationExtension
{
    // 047: MCP mount policy adı öneki — "Platform:<scope>" (default REST scope policy'lerinden ayrı,
    // ZORUNLU "Platform" JwtBearer şemasına bağlı). Program.cs literal yerine bu sabitle policy adı kurar.
    public const string PlatformPolicyPrefix = "Platform:";

    public static IServiceCollection AddAuthenticationAndAuthorizationExtension(this IServiceCollection services,
        IConfiguration configuration, params string[] scopes)
    {
        var identityOptions = configuration.GetSection(nameof(IdentityOption))
            .Get<IdentityOption>()!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = identityOptions.Address;
                options.Audience = identityOptions.Audience;
                options.RequireHttpsMetadata = false;

                // Claim'leri token'daki haliyle birak (scope/role/email kisa adlariyla).
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                };

                options.AutomaticRefreshInterval = TimeSpan.FromHours(24);
                options.RefreshInterval = TimeSpan.FromSeconds(30);
            });

        // 012: tenant policy handler'ları route değerine erişir.
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationHandler, MerchantScopeAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, AdminPlaneOnlyAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            // Her scope icin policy: gecerli (authenticated) token + ilgili "scope" claim'i.
            foreach (var scope in scopes)
                options.AddPolicy(scope, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", scope);
                });

            // 012: claim-tabanlı tenant policy'leri — uçlar scope policy'sinin YANINA açıkça beyan eder.
            options.AddPolicy(AuthorizationPolicies.MerchantScoped, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new MerchantScopeRequirement());
            });
            options.AddPolicy(AuthorizationPolicies.AdminPlaneOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new AdminPlaneOnlyRequirement());
            });
        });

        return services;
    }

    // 047 R3: MCP yüzeyinin İKİNCİ authority'si — AgentPlatform IdP (insan/agent düzlemi). "Platform"
    // adlı ayrı JwtBearer şeması + "Platform:<scope>" policy'leri. MapMcp yalnız bu policy'yi kullanır →
    // PG IdP token'ı MCP'ye giremez, platform token'ı REST'e giremez (FR-005, yüzey başına tek otorite).
    // REST default şeması (PG IdP) DEĞİŞMEZ. Wolverine ScopeAuthorizationMiddleware ClaimsPrincipal
    // okuduğu için tool-bazlı [RequiredScope] son savunması şemadan bağımsız çalışır.
    public static IServiceCollection AddPlatformMcpAuthentication(this IServiceCollection services,
        IConfiguration configuration, params string[] mcpScopes)
    {
        var platform = configuration.GetSection(nameof(PlatformIdentityOption))
            .Get<PlatformIdentityOption>()!;

        services.AddAuthentication()
            .AddJwtBearer("Platform", options =>
            {
                options.Authority = platform.Address;
                options.Audience = platform.Audience;
                options.RequireHttpsMetadata = false;

                // Claim'leri token'daki haliyle birak (scope/role kisa adlariyla).
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                };

                options.AutomaticRefreshInterval = TimeSpan.FromHours(24);
                options.RefreshInterval = TimeSpan.FromSeconds(30);
            });

        services.AddAuthorization(options =>
        {
            foreach (var scope in mcpScopes)
                options.AddPolicy($"{PlatformPolicyPrefix}{scope}", policy =>
                {
                    policy.AddAuthenticationSchemes("Platform");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", scope);
                });
        });

        return services;
    }
}