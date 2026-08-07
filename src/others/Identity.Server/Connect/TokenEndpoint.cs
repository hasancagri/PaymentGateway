using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Server.Connect;

// /connect/token — yalnız client_credentials (029'un alt kümesi; insan akışları yok — D1).
// Grant/secret/scope doğrulamasını OpenIddict yapar; buraya yalnız GEÇERLİ istek düşer.
// G2 bu dala merchant_id claim'i + status-gated scope süzmesini ekleyecek (D9).
public static class TokenEndpoint
{
    public static void MapTokenEndpoint(this WebApplication app) =>
        app.MapPost("/connect/token", HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IOpenIddictScopeManager scopeManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OIDC token isteği çözülemedi.");

        if (!request.IsClientCredentialsGrantType())
            throw new InvalidOperationException("Desteklenmeyen grant type.");

        // M2M: sub = client id (029 paritesi).
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, request.ClientId);

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
}