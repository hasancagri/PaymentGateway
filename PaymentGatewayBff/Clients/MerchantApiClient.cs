namespace PaymentGatewayBff.Clients;

public class MerchantApiClient(HttpClient httpClient)
{
    public Task<HttpResponseMessage> GetAllAsync(CancellationToken ct = default)
        => httpClient.GetAsync("/merchants", ct);

    public Task<HttpResponseMessage> GetByIdAsync(Guid id, CancellationToken ct = default)
        => httpClient.GetAsync($"/merchants/{id}", ct);

    public Task<HttpResponseMessage> CreateAsync(object request, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync("/merchants", request, ct);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, object request, CancellationToken ct = default)
        => httpClient.PutAsJsonAsync($"/merchants/{id}", request, ct);

    public Task<HttpResponseMessage> ActivateAsync(Guid id, object request, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync($"/merchants/{id}/activate", request, ct);

    public Task<HttpResponseMessage> DeactivateAsync(Guid id, object request, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync($"/merchants/{id}/deactivate", request, ct);

    public Task<HttpResponseMessage> SuspendAsync(Guid id, object request, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync($"/merchants/{id}/suspend", request, ct);
}