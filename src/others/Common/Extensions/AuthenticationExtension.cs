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

    // Kimlik birleşimi (2026-10-22): PG kendi OpenIddict IdP'sini bıraktı → REST JWT doğrulama
    // (AddAuthenticationAndAuthorizationExtension, IdentityOption/localhost:5101) SÖKÜLDÜ. EC→PG = API key
    // (ApiKeyAuthentication.cs). Gelecek operatör/MCP/ops JWT yüzeyi için AgentPlatform IdP seam'i AŞAĞIDA.

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