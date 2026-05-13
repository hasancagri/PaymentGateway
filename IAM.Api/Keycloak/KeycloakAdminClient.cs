using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IAM.Api.Keycloak;

public class KeycloakAdminClient : ITransientDependency
{
    private readonly HttpClient _http;
    private readonly KeycloakTokenProvider _tokenProvider;
    private readonly IConfiguration _config;

    public KeycloakAdminClient(
        HttpClient http,
        KeycloakTokenProvider tokenProvider,
        IConfiguration config)
    {
        _http          = http;
        _tokenProvider = tokenProvider;
        _config        = config;
    }

    private string AdminBase =>
        $"{_config["Keycloak:AdminApiBaseUrl"] ?? throw new InvalidOperationException("Keycloak:AdminApiBaseUrl is required.")}" +
        $"/admin/realms/{_config["Keycloak:Realm"] ?? throw new InvalidOperationException("Keycloak:Realm is required.")}";

    public async Task<Guid> CreateUserAsync(
        string email, string password, string firstName, string lastName,
        CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAdminTokenAsync(ct);

        var body = new
        {
            username    = email,
            email,
            firstName,
            lastName,
            enabled     = true,
            credentials = new[]
            {
                new { type = "password", value = password, temporary = false }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{AdminBase}/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        // Location header: .../admin/realms/payment-gateway/users/{id}
        var location = response.Headers.Location!.ToString();
        return Guid.Parse(location.Split('/').Last());
    }

    public async Task ResetPasswordAsync(
        Guid keycloakId, string newPassword, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAdminTokenAsync(ct);
        var body  = new { type = "password", value = newPassword, temporary = false };

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{AdminBase}/users/{keycloakId}/reset-password");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteUserAsync(Guid keycloakId, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAdminTokenAsync(ct);

        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{AdminBase}/users/{keycloakId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}