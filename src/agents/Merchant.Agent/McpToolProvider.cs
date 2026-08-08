namespace Merchant.Agent;

/// <summary>
/// Merchant.Api'nin MCP server'ına bağlanır (Streamable HTTP), tool'ları keşfeder (<c>ListTools</c>)
/// ve Agent Framework'e <see cref="AITool"/> olarak verir (Payment.Agent deseni). Keşif başarısızsa
/// (api henüz ayakta değil) boş liste döner — agent yine başlar, Agent Card sunulur.
/// httpClient AgentTokenHandler'lı gelir — her MCP isteği Bearer taşır (/mcp merchant.write ister).
/// </summary>
public static class McpToolProvider
{
    // MCP client, tool çağrıları için app-ömrü boyunca canlı kalmalı → dispose edilmez (statik tutulur).
    private static readonly List<McpClient> KeepAlive = new();

    public static async Task<IList<AITool>> DiscoverToolsAsync(
        string mcpEndpoint, HttpClient httpClient, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            var client = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions
                    {
                        Name = "merchant-api",
                        Endpoint = new Uri(mcpEndpoint),
                        TransportMode = HttpTransportMode.StreamableHttp
                    },
                    httpClient,
                    ownsHttpClient: false),
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