using System.Text.Json;

namespace Admin.Clients;

/// <summary>
/// Merchant.Agent A2A JSON-RPC istemcisi (019 komisyon pazarlık chat'i). BC API'lerinden farklı:
/// ApiResult zarfı yok, A2A "SendMessage" metodu konuşulur; çok-turlu bağlam contextId ile sürer.
/// A2A yüzeyi auth istemez (agent kendi MCP çağrılarında makine token'ı taşır) → AdminTokenHandler yok.
/// </summary>
public interface IMerchantAgentClient
{
    Task<AgentChatResult> SendAsync(string text, string? contextId, CancellationToken ct = default);
}

/// <summary>Agent cevabı: metin + konuşmayı sürdürmek için contextId (hata halinde IsSuccess=false).</summary>
public record AgentChatResult(bool IsSuccess, string Reply, string? ContextId);

public class MerchantAgentClient : IMerchantAgentClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public MerchantAgentClient(HttpClient http) => _http = http;

    public async Task<AgentChatResult> SendAsync(string text, string? contextId, CancellationToken ct = default)
    {
        // A2A JSON-RPC: method "SendMessage"; part düz {"text": ...}, rol "ROLE_USER".
        var message = new Dictionary<string, object?>
        {
            ["role"] = "ROLE_USER",
            ["messageId"] = Guid.NewGuid().ToString("N"),
            ["parts"] = new[] { new Dictionary<string, string> { ["text"] = text } }
        };
        if (!string.IsNullOrWhiteSpace(contextId))
            message["contextId"] = contextId;

        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "SendMessage",
            @params = new { message }
        };

        try
        {
            using var response = await _http.PostAsJsonAsync("/", request, Json, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var error))
                return new AgentChatResult(false,
                    error.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Agent hatası." : "Agent hatası.",
                    contextId);

            var agentMessage = doc.RootElement.GetProperty("result").GetProperty("message");
            var reply = string.Join("\n", agentMessage.GetProperty("parts").EnumerateArray()
                .Where(p => p.TryGetProperty("text", out _))
                .Select(p => p.GetProperty("text").GetString()));
            var newContextId = agentMessage.TryGetProperty("contextId", out var cid)
                ? cid.GetString()
                : contextId;

            return new AgentChatResult(true, reply, newContextId);
        }
        catch
        {
            return new AgentChatResult(false, "Merchant.Agent'a ulaşılamadı.", contextId);
        }
    }
}
