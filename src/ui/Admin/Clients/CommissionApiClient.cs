using System.Net.Http.Json;

namespace Admin.Clients;

public interface ICommissionApiClient
{
    Task<ApiResult<IdResult>> CreateBankCommissionAsync(CreateBankCommissionRequest request, CancellationToken ct = default);
    Task<ApiResult<BankCommissionsResponse>> GetBankCommissionsAsync(string? bankCode = null, CancellationToken ct = default);
    Task<ApiResult<IdResult>> CreateMerchantCommissionAsync(CreateMerchantCommissionRequest request, CancellationToken ct = default);
    Task<ApiResult<IdResult>> UpdateMerchantCommissionAsync(Guid id, UpdateMerchantCommissionRequest request, CancellationToken ct = default);
    Task<ApiResult<MerchantCommissionsResponse>> GetMerchantCommissionsAsync(Guid merchantId, CancellationToken ct = default);
}

public class CommissionApiClient : ApiClientBase, ICommissionApiClient
{
    public CommissionApiClient(HttpClient http) : base(http)
    {
    }

    public Task<ApiResult<IdResult>> CreateBankCommissionAsync(CreateBankCommissionRequest request, CancellationToken ct = default) =>
        SendAsync<IdResult>(() => Http.PostAsJsonAsync("/api/v1/bank-commissions", request, ct), ct);

    public Task<ApiResult<BankCommissionsResponse>> GetBankCommissionsAsync(string? bankCode = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(bankCode)
            ? "/api/v1/bank-commissions"
            : $"/api/v1/bank-commissions?bankCode={Uri.EscapeDataString(bankCode)}";
        return SendAsync<BankCommissionsResponse>(() => Http.GetAsync(url, ct), ct);
    }

    public Task<ApiResult<IdResult>> CreateMerchantCommissionAsync(CreateMerchantCommissionRequest request, CancellationToken ct = default) =>
        SendAsync<IdResult>(() => Http.PostAsJsonAsync("/api/v1/merchant-commissions", request, ct), ct);

    public Task<ApiResult<IdResult>> UpdateMerchantCommissionAsync(Guid id, UpdateMerchantCommissionRequest request, CancellationToken ct = default) =>
        SendAsync<IdResult>(() => Http.PutAsJsonAsync($"/api/v1/merchant-commissions/{id}", request, ct), ct);

    public Task<ApiResult<MerchantCommissionsResponse>> GetMerchantCommissionsAsync(Guid merchantId, CancellationToken ct = default) =>
        SendAsync<MerchantCommissionsResponse>(() => Http.GetAsync($"/api/v1/merchant-commissions?merchantId={merchantId}", ct), ct);
}