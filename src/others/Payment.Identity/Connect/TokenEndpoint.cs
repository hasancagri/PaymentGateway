using System.Security.Claims;
using Payment.Identity.EventHandlers;
using Microsoft.AspNetCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Payment.Identity.Connect;

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
        IOpenIddictApplicationManager applicationManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OIDC token isteği çözülemedi.");

        // 047+048: yalnız M2M. authorization_code/refresh_token (insan) dalı SÖKÜLDÜ — auth-code
        // istemci kalmadı (external-admin-agent AgentPlatform IdP'ye taşındı); OpenIddict yalnız
        // client_credentials'ı kabul eder, başka grant zaten buraya düşmez.
        if (request.IsClientCredentialsGrantType())
        {
            // M2M: sub = client id (029 paritesi).
            var identity = new ClaimsIdentity(
                TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

            identity.SetClaim(Claims.Subject, request.ClientId);

            // 012 (D6): merchant istemcilerinde application Properties'teki merchant_id access token'a
            // claim olarak girer; statik istemcilerde (admin-ui) property yok → claim yok.
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

        throw new InvalidOperationException("Desteklenmeyen grant type.");
    }
}
