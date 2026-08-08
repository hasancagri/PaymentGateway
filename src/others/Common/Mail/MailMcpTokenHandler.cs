using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Common.Mail;

/// <summary>
/// Mail.Mcp <c>/mcp</c> çağrılarına <c>mail.send</c> scope'lu makine token'ı takar (AgentTokenHandler
/// deseni — 013 D7). client_credentials token'ı static cache'lenir, süresine 30 sn kala yenilenir.
/// İstemci kimliği config'ten (<c>MailAuth:ClientId</c>/<c>Secret</c>; mail atan BC başına Identity client).
/// </summary>
public sealed class MailMcpTokenHandler(IConfiguration config) : DelegatingHandler
{
    private const string Scopes = "mail.send";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string? _token;
    private static DateTimeOffset _expiresAt;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30))
            return _token;

        await Gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30))
                return _token;

            var authority = config["IdentityOption:Address"]!;
            using var http = new HttpClient();
            using var response = await http.PostAsync($"{authority}/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = config["MailAuth:ClientId"]!,
                    ["client_secret"] = config["MailAuth:ClientSecret"]!,
                    ["scope"] = Scopes
                }), ct);
            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            _token = json.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = json.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return _token;
        }
        finally
        {
            Gate.Release();
        }
    }
}