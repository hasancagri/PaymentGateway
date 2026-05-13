namespace IAM.Api.Keycloak;

public class KeycloakTokenProvider : ITransientDependency
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public KeycloakTokenProvider(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string> GetAdminTokenAsync(CancellationToken ct = default)
    {
        var baseUrl = _config["Keycloak:AdminApiBaseUrl"]!;
        var realm   = _config["Keycloak:Realm"]!;
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = _config["Keycloak:ClientId"]!,
            ["client_secret"] = _config["Keycloak:ClientSecret"]!,
        };

        var response = await _http.PostAsync(
            $"{baseUrl}/realms/{realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(form), ct);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.GetProperty("access_token").GetString()!;
    }
}