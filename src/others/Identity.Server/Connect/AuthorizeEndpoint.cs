using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Server.Connect;

// /connect/authorize — code+PKCE akışının kullanıcı etkileşim ucu. Tek admin, seed istemci
// (Implicit consent) — consent dalı YOK (YAGNI; DCR/çoklu-kullanıcı gelirse ayrı spec ekler).
public static class AuthorizeEndpoint
{
    public static void MapAuthorizeEndpoint(this WebApplication app) =>
        app.MapMethods("/connect/authorize", ["GET", "POST"], HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IOpenIddictScopeManager scopeManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OIDC authorize isteği çözülemedi.");

        // Cookie ile kimlik doğrula. Yoksa login'e yönlendir (returnUrl bu isteğin kendisi).
        var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (result is not { Succeeded: true })
        {
            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                [IdentityConstants.ApplicationScheme]);
        }

        var user = await userManager.GetUserAsync(result.Principal!)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user));
        identity.SetClaim(Claims.Name, await userManager.GetUserNameAsync(user));
        identity.SetClaim(Claims.Email, await userManager.GetEmailAsync(user));

        // Tek admin, tek istemci — requested scope'lar OpenIddict tarafından zaten istemcinin
        // kayıtlı Scopes listesine göre süzülmüştür (built-in validation); ek filtre gerekmez.
        identity.SetScopes(request.GetScopes());

        var resources = new List<string>();
        await foreach (var resource in scopeManager.ListResourcesAsync(identity.GetScopes()))
            resources.Add(resource);
        identity.SetResources(resources);

        identity.SetDestinations(OidcClaimDestinations.GetDestinations);

        return Results.SignIn(new ClaimsPrincipal(identity), null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
