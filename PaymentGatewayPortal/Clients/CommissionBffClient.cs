using System.Net.Http.Json;
using PaymentGatewayPortal.Models;

namespace PaymentGatewayPortal.Clients;

public class CommissionBffClient(HttpClient httpClient)
{
    public Task<BffResult<List<MerchantCommissionListItem>>?> GetMerchantCommissionsAsync(Guid merchantId, CancellationToken ct = default)
        => httpClient.GetFromJsonAsync<BffResult<List<MerchantCommissionListItem>>>($"/web/merchant-commissions?merchantId={merchantId}", ct);

    public Task<BffResult<List<BankCommissionListItem>>?> GetBankCommissionsAsync(Guid? bankId = null, CancellationToken ct = default)
    {
        var url = bankId.HasValue ? $"/web/bank-commissions?bankId={bankId}" : "/web/bank-commissions";
        return httpClient.GetFromJsonAsync<BffResult<List<BankCommissionListItem>>>(url, ct);
    }

    public Task<HttpResponseMessage> DefineMerchantCommissionAsync(DefineMerchantCommissionRequest request, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync("/web/merchant-commissions", request, ct);

    public Task<HttpResponseMessage> UpdateCommissionRateAsync(Guid id, decimal newRate, CancellationToken ct = default)
        => httpClient.PatchAsJsonAsync($"/web/merchant-commissions/{id}/rate", new { CommissionId = id, NewRate = newRate }, ct);
}