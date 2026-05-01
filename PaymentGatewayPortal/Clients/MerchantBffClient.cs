using System.Net.Http.Json;
using PaymentGatewayPortal.Models;

namespace PaymentGatewayPortal.Clients;

public class MerchantBffClient(HttpClient httpClient)
{
    public Task<BffResult<List<MerchantListItem>>?> GetAllAsync(CancellationToken ct = default)
        => httpClient.GetFromJsonAsync<BffResult<List<MerchantListItem>>>("/web/merchants", ct);

    public Task<BffResult<MerchantDetail>?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => httpClient.GetFromJsonAsync<BffResult<MerchantDetail>>($"/web/merchants/{id}", ct);

    public Task<HttpResponseMessage> CreateAsync(CreateMerchantRequest request, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync("/web/merchants", request, ct);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateMerchantRequest request, CancellationToken ct = default)
        => httpClient.PutAsJsonAsync($"/web/merchants/{id}", request, ct);

    public Task<HttpResponseMessage> ActivateAsync(Guid id, string reason, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync($"/web/merchants/{id}/activate", new { Reason = reason }, ct);

    public Task<HttpResponseMessage> DeactivateAsync(Guid id, string reason, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync($"/web/merchants/{id}/deactivate", new { Reason = reason }, ct);

    public Task<HttpResponseMessage> SuspendAsync(Guid id, string reason, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync($"/web/merchants/{id}/suspend", new { Reason = reason }, ct);
}