using System.Security.Claims;
using System.Text.Encodings.Web;
using Common.Utils.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Payment.Api.Auth;

/// <summary>
/// 039: yapısal çekim/retrieve uçları için X-Api-Key auth şeması. ECom Order.Api statik X-Api-Key
/// header'ı (merchant key) taşır — kullanıcı JWT'si değil (İlke I: agent-olmayan server-to-server).
/// Anahtar SHA-256'lanıp <see cref="MerchantApiKeyReference"/> ile aranır → merchant çözülür →
/// <c>merchant_id</c> claim'i set edilir. Böylece mevcut <c>MerchantScoped</c> policy (claim==route)
/// DEĞİŞMEDEN çalışır. Header yoksa NoResult (JWT şeması denenebilir); anahtar geçersizse Fail.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IQuerySession session)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return AuthenticateResult.NoResult(); // header yok → bu şema uygulanmaz

        var apiKey = values.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
            return AuthenticateResult.Fail("Empty API key");

        var hash = ApiKeyHash.Compute(apiKey);
        var reference = await session.Query<MerchantApiKeyReference>()
            .FirstOrDefaultAsync(x => x.KeyHash == hash);
        if (reference is null)
            return AuthenticateResult.Fail("Unknown API key");

        var claims = new[]
        {
            new Claim(MerchantScopeAuthorizationHandler.MerchantIdClaim, reference.Id.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
