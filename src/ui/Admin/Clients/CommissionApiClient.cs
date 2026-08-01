using System.Net.Http.Json;

namespace Admin.Clients;

public interface ICommissionApiClient
{
    Task<ApiResult<IdResult>> CreateBankCommissionAsync(CreateBankCommissionRequest request, CancellationToken ct = default);
    Task<ApiResult<BankCommissionsResponse>> GetBankCommissionsAsync(string? bankCode = null, CancellationToken ct = default);
    Task<ApiResult<CriteriaOptions>> GetCriteriaOptionsAsync(CancellationToken ct = default);
    Task<ApiResult<BulkBankCommissionsResult>> BulkUpsertBankCommissionsAsync(BulkBankCommissionsRequest request, CancellationToken ct = default);
    Task<ApiResult<CodeResult>> CreateBankAsync(CreateBankRequest request, CancellationToken ct = default);
    Task<ApiResult<BanksResponse>> GetBanksAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<ApiResult<BankCatalogResponse>> GetBankCatalogAsync(bool onlyAvailable = true, CancellationToken ct = default);
    Task<ApiResult<BankDetail>> GetBankAsync(string code, CancellationToken ct = default);
    Task<ApiResult<CodeResult>> UpdateBankAsync(string code, UpdateBankRequest request, CancellationToken ct = default);
    Task<ApiResult<CodeResult>> DeleteBankAsync(string code, CancellationToken ct = default);
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

    public Task<ApiResult<CriteriaOptions>> GetCriteriaOptionsAsync(CancellationToken ct = default) =>
        SendAsync<CriteriaOptions>(() => Http.GetAsync("/api/v1/bank-commissions/criteria-options", ct), ct);

    public Task<ApiResult<BulkBankCommissionsResult>> BulkUpsertBankCommissionsAsync(BulkBankCommissionsRequest request, CancellationToken ct = default) =>
        SendAsync<BulkBankCommissionsResult>(() => Http.PostAsJsonAsync("/api/v1/bank-commissions/bulk", request, ct), ct);

    public Task<ApiResult<CodeResult>> CreateBankAsync(CreateBankRequest request, CancellationToken ct = default) =>
        SendAsync<CodeResult>(() => Http.PostAsJsonAsync("/api/v1/banks", request, ct), ct);

    public Task<ApiResult<BanksResponse>> GetBanksAsync(bool includeInactive = false, CancellationToken ct = default) =>
        SendAsync<BanksResponse>(() => Http.GetAsync($"/api/v1/banks?includeInactive={(includeInactive ? "true" : "false")}", ct), ct);

    public Task<ApiResult<BankCatalogResponse>> GetBankCatalogAsync(bool onlyAvailable = true, CancellationToken ct = default) =>
        SendAsync<BankCatalogResponse>(() => Http.GetAsync($"/api/v1/banks/catalog?onlyAvailable={(onlyAvailable ? "true" : "false")}", ct), ct);

    public Task<ApiResult<BankDetail>> GetBankAsync(string code, CancellationToken ct = default) =>
        SendAsync<BankDetail>(() => Http.GetAsync($"/api/v1/banks/{Uri.EscapeDataString(code)}", ct), ct);

    public Task<ApiResult<CodeResult>> UpdateBankAsync(string code, UpdateBankRequest request, CancellationToken ct = default) =>
        SendAsync<CodeResult>(() => Http.PutAsJsonAsync($"/api/v1/banks/{Uri.EscapeDataString(code)}", request, ct), ct);

    public Task<ApiResult<CodeResult>> DeleteBankAsync(string code, CancellationToken ct = default) =>
        SendAsync<CodeResult>(() => Http.DeleteAsync($"/api/v1/banks/{Uri.EscapeDataString(code)}", ct), ct);

    public Task<ApiResult<IdResult>> CreateMerchantCommissionAsync(CreateMerchantCommissionRequest request, CancellationToken ct = default) =>
        SendAsync<IdResult>(() => Http.PostAsJsonAsync("/api/v1/merchant-commissions", request, ct), ct);

    public Task<ApiResult<IdResult>> UpdateMerchantCommissionAsync(Guid id, UpdateMerchantCommissionRequest request, CancellationToken ct = default) =>
        SendAsync<IdResult>(() => Http.PutAsJsonAsync($"/api/v1/merchant-commissions/{id}", request, ct), ct);

    public Task<ApiResult<MerchantCommissionsResponse>> GetMerchantCommissionsAsync(Guid merchantId, CancellationToken ct = default) =>
        SendAsync<MerchantCommissionsResponse>(() => Http.GetAsync($"/api/v1/merchant-commissions?merchantId={merchantId}", ct), ct);
}