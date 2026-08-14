using System.Net.Http.Json;

namespace Admin.Clients;

public interface IMerchantApiClient
{
    Task<ApiResult<MerchantDetail>> GetAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<MerchantsResponse>> GetAllAsync(CancellationToken ct = default);
}

public class MerchantApiClient : ApiClientBase, IMerchantApiClient
{
    public MerchantApiClient(HttpClient http) : base(http)
    {
    }

    public Task<ApiResult<MerchantDetail>> GetAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<MerchantDetail>(() => Http.GetAsync($"/api/v1/merchants/{id}", ct), ct);

    public Task<ApiResult<MerchantsResponse>> GetAllAsync(CancellationToken ct = default) =>
        SendAsync<MerchantsResponse>(() => Http.GetAsync("/api/v1/merchants", ct), ct);
}