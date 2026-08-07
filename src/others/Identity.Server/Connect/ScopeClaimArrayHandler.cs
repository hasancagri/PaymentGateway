using System.Security.Claims;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Server.Connect;

// R3 (EN KRİTİK — 029'dan birebir): OpenIddict access token'a scope'u RFC 9068 gereği tek
// boşluk-ayrık string yazar; servisler ise RequireClaim("scope", x) ile TEK TEK değer arar →
// tek string'te sessizce 403 (fail-closed; 029'da canlı yaşandı). JWT'de scope'u JSON DİZİSİ
// yaparsak JsonWebTokenHandler her elemanı ayrı "scope" claim'ine açar. Yalnız ACCESS TOKEN'a dokunur.
public sealed class ScopeClaimArrayHandler : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    // AttachTokenPayload descriptor'ı doldurduktan SONRA, GenerateIdentityModelToken token'ı
    // üretmeden ÖNCE çalışsın diye onun bir önüne sıralanır.
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.GenerateTokenContext>()
            .UseSingletonHandler<ScopeClaimArrayHandler>()
            .SetOrder(OpenIddictServerHandlers.Protection.GenerateIdentityModelToken.Descriptor.Order - 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Yalnız access token; diğer token türleri dokunulmaz.
        // NOT: context.TokenType URN'dir (TokenTypeIdentifiers.AccessToken), kısa hint (TokenTypeHints) DEĞİL —
        // TokenTypeHints ile kıyas bilinen sessiz no-op tuzağı.
        if (context.TokenType is not TokenTypeIdentifiers.AccessToken)
            return default;

        var descriptor = context.SecurityTokenDescriptor;
        if (descriptor is not null &&
            descriptor.Claims.TryGetValue(Claims.Scope, out var value) &&
            value is string scope &&
            !string.IsNullOrEmpty(scope))
        {
            // Tek "a b c" string'i → ["a","b","c"]; JWT payload'ında JSON dizisi olur.
            descriptor.Claims[Claims.Scope] = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        return default;
    }
}

// Claim destinasyonları — M2M hali: id_token yok (insan akışı yok), her claim access token'a.
public static class OidcClaimDestinations
{
    public static IEnumerable<string> GetDestinations(Claim claim) => [Destinations.AccessToken];
}