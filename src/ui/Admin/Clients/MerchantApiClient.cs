using System.Net.Http.Json;

namespace Admin.Clients;

// 044: Admin CRUD ekranları söküldü (yönetim MCP'de) — istemci yalnız hassas-veri sayfasının
// dar BFF çiftini çağırır (AdminPlaneOnly uçlar).
public interface IMerchantApiClient
{
    Task<ApiResult<MerchantSensitiveDetail>> GetSensitiveAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<MerchantSensitiveDetail>> UpdateSensitiveAsync(
        Guid id, UpdateMerchantSensitiveRequest request, CancellationToken ct = default);
}

public class MerchantApiClient : ApiClientBase, IMerchantApiClient
{
    public MerchantApiClient(HttpClient http) : base(http)
    {
    }

    public Task<ApiResult<MerchantSensitiveDetail>> GetSensitiveAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<MerchantSensitiveDetail>(() => Http.GetAsync($"/api/v1/merchants/{id}/sensitive", ct), ct);

    public Task<ApiResult<MerchantSensitiveDetail>> UpdateSensitiveAsync(
        Guid id, UpdateMerchantSensitiveRequest request, CancellationToken ct = default) =>
        SendAsync<MerchantSensitiveDetail>(() => Http.PutAsJsonAsync($"/api/v1/merchants/{id}/sensitive", request, ct), ct);
}