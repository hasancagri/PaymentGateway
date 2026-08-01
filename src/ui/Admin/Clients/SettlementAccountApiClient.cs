using System.Net.Http.Json;

namespace Admin.Clients;

/// <summary>
/// Merchant settlement hesapları API'si (004). Rotalar merchant-scoped:
/// <c>/api/v1/merchants/{merchantId}/settlement-accounts</c>. Tenant sınırı her çağrıda
/// <c>merchantId</c> ile taşınır. <c>MerchantApiClient</c> deseniyle aynı.
/// </summary>
public interface ISettlementAccountApiClient
{
    Task<ApiResult<SettlementAccountsResponse>> GetAccountsAsync(Guid merchantId, CancellationToken ct = default);
    Task<ApiResult<SettlementAccountDetail>> GetAccountAsync(Guid merchantId, Guid accountId, CancellationToken ct = default);
    Task<ApiResult<IdResult>> CreateAsync(Guid merchantId, CreateSettlementAccountRequest request, CancellationToken ct = default);
    Task<ApiResult<IdResult>> UpdateAsync(Guid merchantId, Guid accountId, UpdateSettlementAccountRequest request, CancellationToken ct = default);
    Task<ApiResult<IdStatusResult>> SetStatusAsync(Guid merchantId, Guid accountId, SetSettlementAccountStatusRequest request, CancellationToken ct = default);
}

public class SettlementAccountApiClient : ApiClientBase, ISettlementAccountApiClient
{
    public SettlementAccountApiClient(HttpClient http) : base(http)
    {
    }

    private static string Base(Guid merchantId) =>
        $"/api/v1/merchants/{merchantId}/settlement-accounts";

    public Task<ApiResult<SettlementAccountsResponse>> GetAccountsAsync(Guid merchantId, CancellationToken ct = default) =>
        SendAsync<SettlementAccountsResponse>(() => Http.GetAsync(Base(merchantId), ct), ct);

    public Task<ApiResult<SettlementAccountDetail>> GetAccountAsync(Guid merchantId, Guid accountId, CancellationToken ct = default) =>
        SendAsync<SettlementAccountDetail>(() => Http.GetAsync($"{Base(merchantId)}/{accountId}", ct), ct);

    public Task<ApiResult<IdResult>> CreateAsync(Guid merchantId, CreateSettlementAccountRequest request, CancellationToken ct = default) =>
        SendAsync<IdResult>(() => Http.PostAsJsonAsync(Base(merchantId), request, ct), ct);

    public Task<ApiResult<IdResult>> UpdateAsync(Guid merchantId, Guid accountId, UpdateSettlementAccountRequest request, CancellationToken ct = default) =>
        SendAsync<IdResult>(() => Http.PutAsJsonAsync($"{Base(merchantId)}/{accountId}", request, ct), ct);

    public Task<ApiResult<IdStatusResult>> SetStatusAsync(Guid merchantId, Guid accountId, SetSettlementAccountStatusRequest request, CancellationToken ct = default) =>
        SendAsync<IdStatusResult>(() => Http.PutAsJsonAsync($"{Base(merchantId)}/{accountId}/status", request, ct), ct);
}