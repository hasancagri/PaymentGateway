namespace Payment.Agent;

/// <summary>
/// Payment.Api'nin MCP server'ına bağlanır (Streamable HTTP), tool'ları keşfeder (<c>ListTools</c>)
/// ve Agent Framework'e <see cref="AITool"/> olarak verir (<c>McpClientTool : AIFunction</c> köprüsü;
/// adaptör gerekmez). Keşif başarısızsa (api henüz ayakta değil) boş liste döner — agent yine başlar,
/// Agent Card sunulur. Auth 007'de yok → ECommerce'deki per-user session katmanı gerekmez.
/// </summary>
public static class McpToolProvider
{
    // MCP client, tool çağrıları için app-ömrü boyunca canlı kalmalı → dispose edilmez (statik tutulur).
    private static readonly List<McpClient> KeepAlive = new();

    public static async Task<IList<AITool>> DiscoverToolsAsync(string mcpEndpoint, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            var client = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions
                {
                    Name = "payment-api",
                    Endpoint = new Uri(mcpEndpoint),
                    TransportMode = HttpTransportMode.StreamableHttp
                }),
                cancellationToken: ct);

            KeepAlive.Add(client);

            var tools = await client.ListToolsAsync(cancellationToken: ct);
            logger.LogInformation("MCP '{Endpoint}': {Count} tool keşfedildi.", mcpEndpoint, tools.Count);
            return tools.Cast<AITool>().ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP '{Endpoint}' tool keşfi başarısız; agent tool'suz başlıyor.", mcpEndpoint);
            return [];
        }
    }
}