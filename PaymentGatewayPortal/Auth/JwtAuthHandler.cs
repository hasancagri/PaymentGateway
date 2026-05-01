using System.Net.Http.Headers;

namespace PaymentGatewayPortal.Auth;

public class JwtAuthHandler(JwtTokenStore tokenStore) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(tokenStore.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.Token);
        return base.SendAsync(request, ct);
    }
}