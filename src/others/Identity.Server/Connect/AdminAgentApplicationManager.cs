using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;

namespace Identity.Server.Connect;

// G3: YALNIZ seed'li yönetim istemcisi (external-admin-agent) için RFC 8252 §7.3 loopback
// redirect muafiyeti — MCP Inspector/CLI istemcileri dinamik portlu http://localhost|127.0.0.1
// callback'i kullanır, seed'e port yazılamaz. Diğer TÜM istemciler birebir eşleşme kuralında kalır.
public sealed class AdminAgentApplicationManager<TApplication>(
    IOpenIddictApplicationCache<TApplication> cache,
    ILogger<OpenIddictApplicationManager<TApplication>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> options,
    IOpenIddictApplicationStore<TApplication> store)
    : OpenIddictApplicationManager<TApplication>(cache, logger, options, store)
    where TApplication : class
{
    public override async ValueTask<bool> ValidateRedirectUriAsync(
        TApplication application, string uri, CancellationToken cancellationToken = default)
    {
        if (await base.ValidateRedirectUriAsync(application, uri, cancellationToken))
            return true;

        var clientId = await GetClientIdAsync(application, cancellationToken);
        if (clientId != Config.ExternalAdminAgentClientId)
            return false;

        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
               && parsed.Scheme == Uri.UriSchemeHttp
               && parsed.Host is "localhost" or "127.0.0.1";
    }
}
