namespace PaymentGatewayPortal.Clients;

public class AuthBffClient(HttpClient httpClient)
{
    public record LoginRequest(string Email, string Password);

    public record LoginResponse(bool IsSuccess, LoginData? Data);

    public record LoginData(string Token);

    public Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync("/web/auth/login", new LoginRequest(email, password), ct)
            .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct).Result, ct);
}