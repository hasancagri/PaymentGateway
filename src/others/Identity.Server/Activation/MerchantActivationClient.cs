using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Identity.Server.Activation;

/// <summary>
/// Aktivasyon sayfasının Merchant.Api <c>redeem</c> ucuna senkron çağrısı (013 sanksiyonlu cross-servis
/// çağrı). client_credentials token'ı (identity-activation, merchant.write, claim'siz → AdminPlaneOnly)
/// static cache'lenir (−30sn yenileme). Key custody tutmaz; yalnız redeem yanıtını taşır.
/// </summary>
public sealed class MerchantActivationClient(HttpClient http, IConfiguration config)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string? _token;
    private static DateTimeOffset _expiresAt;

    public record RedeemResult(bool Success, Guid MerchantId, string? MerchantKey, string? Error);

    public async Task<RedeemResult> RedeemAsync(string activationToken, CancellationToken ct)
    {
        try
        {
            var token = await GetTokenAsync(ct);
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/merchants/activation/redeem")
            {
                Content = JsonContent.Create(new { activationToken })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return new RedeemResult(false, Guid.Empty, null, "Aktivasyon başarısız (bilet geçersiz veya süresi dolmuş).");

            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            return new RedeemResult(
                true,
                root.TryGetProperty("merchantId", out var mid) ? mid.GetGuid() : Guid.Empty,
                root.TryGetProperty("merchantKey", out var key) ? key.GetString() : null,
                null);
        }
        catch (Exception ex)
        {
            return new RedeemResult(false, Guid.Empty, null, ex.Message);
        }
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

            var authority = config["IdentityOption:Address"] ?? "https://localhost:5101";
            using var tokenHttp = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

            using var resp = await tokenHttp.PostAsync($"{authority}/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = "identity-activation",
                    ["client_secret"] = config["Clients:identity-activation:Secret"]!,
                    ["scope"] = "merchant.write"
                }), ct);
            resp.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            _token = json.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = json.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 900;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return _token;
        }
        finally
        {
            Gate.Release();
        }
    }
}
