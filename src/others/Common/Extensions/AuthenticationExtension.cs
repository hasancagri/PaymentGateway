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
}