using OpenIddict.Server;

namespace Payment.Identity.Connect;

// G3: MCP istemcileri (Claude Desktop / mcp-remote) RFC 8707 `resource` parametresiyle MCP URL'i
// gönderir. OpenIddict bu değeri scope'lara bağlı resource'ların (merchant.api gibi mantıksal
// audience adları) alt kümesi olarak doğrular → invalid_target. Audience zaten scope→resource
// eşlemesinden üretildiği için `resource` parametresi YOK SAYILIR.
public static class IgnoreResourceParameterHandler
{
    // 047+048: ForAuthorization SÖKÜLDÜ — authorization endpoint + insan akışı kalmadı; yalnız token.
    public sealed class ForToken
        : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
    {
        public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
            OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
                .UseSingletonHandler<ForToken>()
                .SetOrder(int.MinValue + 100_000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build();

        public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Request.Resources = null;
            return default;
        }
    }
}
