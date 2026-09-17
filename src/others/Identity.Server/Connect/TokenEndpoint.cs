using System.Security.Claims;
using Identity.Server.EventHandlers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Server.Connect;

// /connect/token — client_credentials (M2M, 029'un alt kümesi) + authorization_code/refresh_token
// (G3: insan/admin akışları, 042). Grant/secret/scope doğrulamasını OpenIddict yapar; buraya yalnız
// GEÇERLİ istek düşer.
// G2 client_credentials daline merchant_id claim'i + status-gated scope süzmesini ekleyecek (D9).
public static class TokenEndpoint
{
    public static void MapTokenEndpoint(this WebApplication app) =>
        app.MapPost("/connect/token", HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IOpenIddictScopeManager scopeManager,
        IOpenIddictApplicationManager applicationManager,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OIDC token isteği çözülemedi.");

        if (request.IsClientCredentialsGrantType())
        {
            // M2M: sub = client id (029 paritesi).
            var identity = new ClaimsIdentity(
                TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

            identity.SetClaim(Claims.Subject, request.ClientId);

            // 012 (D6): merchant istemcilerinde application Properties'teki merchant_id access token'a
            // claim olarak girer; statik istemcilerde (admin-ui, payment-agent) property yok → claim yok.
            var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
                ?? throw new InvalidOperationException("İstemci kaydı bulunamadı.");
            var properties = await applicationManager.GetPropertiesAsync(application);
            if (properties.TryGetValue(MerchantClientEventHandler.MerchantIdProperty, out var merchantId))
                identity.SetClaim(MerchantClientEventHandler.MerchantIdProperty, merchantId.GetString());

            identity.SetScopes(request.GetScopes());

            // Scope → resource eşlemesinden 'aud' üretilir (servislerin ValidateAudience'ı bunu arar).
            var resources = new List<string>();
            await foreach (var resource in scopeManager.ListResourcesAsync(identity.GetScopes()))
                resources.Add(resource);
            identity.SetResources(resources);

            identity.SetDestinations(OidcClaimDestinations.GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity), null,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // G3: insan akışları — OpenIddict'in code/refresh token'da sakladığı principal'ı geri al.
        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var authResult = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!authResult.Succeeded || authResult.Principal is not { } principal)
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Token süresi geçmiş veya iptal edilmiş.",
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            var user = await userManager.FindByIdAsync(principal.GetClaim(Claims.Subject)!);
            if (user is null || !await signInManager.CanSignInAsync(user))
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Kullanıcı artık giriş yapamıyor.",
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            var identity = new ClaimsIdentity(principal.Claims,
                TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
            identity.SetDestinations(OidcClaimDestinations.GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity), null,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("Desteklenmeyen grant type.");
    }
}
