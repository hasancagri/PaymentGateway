namespace PaymentGatewayBff.Clients;

public class AuthApiClient(HttpClient httpClient)
{
    public Task<HttpResponseMessage> LoginAsync(object request, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync("/auth/login", request, ct);
}