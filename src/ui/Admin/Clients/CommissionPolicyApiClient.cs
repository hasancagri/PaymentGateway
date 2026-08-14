using System.Net.Http.Json;

namespace Admin.Clients;

public interface ICommissionPolicyApiClient
{
    Task<ApiResult<CommissionPoliciesResponse>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResult<CommissionPolicyResult>> CreateAsync(CreateCommissionPolicyRequest request, CancellationToken ct = default);
    Task<ApiResult<CommissionPolicyResult>> UpdateMarginAsync(Guid merchantId, decimal ratePercent, decimal fixedFee, CancellationToken ct = default);
    Task<ApiResult<CommissionPolicyStatusResult>> ChangeStatusAsync(Guid merchantId, string status, CancellationToken ct = default);
}

public class CommissionPolicyApiClient : ApiClientBase, ICommissionPolicyApiClient
{
    public CommissionPolicyApiClient(HttpClient http) : base(http)
    {
    }

    public Task<ApiResult<CommissionPoliciesResponse>> GetAllAsync(CancellationToken ct = default) =>
        SendAsync<CommissionPoliciesResponse>(() => Http.GetAsync("/api/v1/commission-policies", ct), ct);

    public Task<ApiResult<CommissionPolicyResult>> CreateAsync(CreateCommissionPolicyRequest request, CancellationToken ct = default) =>
        SendAsync<CommissionPolicyResult>(() => Http.PostAsJsonAsync("/api/v1/commission-policies", request, ct), ct);

    public Task<ApiResult<CommissionPolicyResult>> UpdateMarginAsync(Guid merchantId, decimal ratePercent, decimal fixedFee, CancellationToken ct = default) =>
        SendAsync<CommissionPolicyResult>(() =>
            Http.PutAsJsonAsync($"/api/v1/commission-policies/{merchantId}/margin",
                new { merchantId, ratePercent, fixedFee }, ct), ct);

    public Task<ApiResult<CommissionPolicyStatusResult>> ChangeStatusAsync(Guid merchantId, string status, CancellationToken ct = default) =>
        SendAsync<CommissionPolicyStatusResult>(() =>
            Http.PutAsJsonAsync($"/api/v1/commission-policies/{merchantId}/status",
                new { merchantId, status }, ct), ct);
}