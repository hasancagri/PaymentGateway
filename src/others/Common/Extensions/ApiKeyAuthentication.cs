using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Common.Options;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common.Extensions;

// EC→PG dış çağrı kimliği: paylaşılan API key (X-Api-Key). OpenIddict/JWT DEĞİL (hafif, IdP'siz).
// Gelen key secret-store'daki beklenen değerle SABİT-ZAMANLI karşılaştırılır (timing-attack guard).
// Key log/trace'e düşmez (İlke VI). Başarıda tek "store" principal; başarısızda 401.
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "StoreApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly StoreApiKeyOptions _keyOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        StoreApiKeyOptions keyOptions)
        : base(options, logger, encoder) => _keyOptions = keyOptions;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrEmpty(provided))
            return Task.FromResult(AuthenticateResult.NoResult()); // key yok → anonim (401 challenge'a düşer)

        var providedBytes = Encoding.UTF8.GetBytes(provided.ToString());
        var expectedBytes = Encoding.UTF8.GetBytes(_keyOptions.ExpectedKey);

        // Farklı uzunlukta false döner (atmaz); sabit-zamanlı → key uzunluğu/değeri sızmaz.
        if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            return Task.FromResult(AuthenticateResult.Fail("invalid api key"));

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "store") }, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public static class ApiKeyAuthenticationExtension
{
    // EC→PG API-key şeması + "store-api-key" policy. StoreApiKeyOptions secret-store'dan (ValidateOnStart).
    public static IServiceCollection AddStoreApiKeyAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StoreApiKeyOptions>()
            .BindConfiguration(nameof(StoreApiKeyOptions))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<StoreApiKeyOptions>>().Value);

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);

        services.AddAuthorization(options =>
            options.AddPolicy(AuthorizationPolicies.StoreApiKey, policy =>
            {
                policy.AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName);
                policy.RequireAuthenticatedUser();
            }));

        return services;
    }
}
