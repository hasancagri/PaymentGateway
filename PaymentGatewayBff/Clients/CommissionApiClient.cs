namespace PaymentGatewayBff.Clients;

public class CommissionApiClient(HttpClient httpClient)
{
    public Task<HttpResponseMessage> GetMerchantCommissionsAsync(Guid merchantId, int page, int pageSize,
        CancellationToken ct = default)
        => httpClient.GetAsync($"/merchant-commissions?merchantId={merchantId}&page={page}&pageSize={pageSize}", ct);

    public Task<HttpResponseMessage> GetMerchantCommissionByIdAsync(Guid id, CancellationToken ct = default)
        => httpClient.GetAsync($"/merchant-commissions/{id}", ct);

    public Task<HttpResponseMessage> DefineMerchantCommissionAsync(object request, CancellationToken ct = default)
        => httpClient.PostAsJsonAsync("/merchant-commissions", request, ct);

    public Task<HttpResponseMessage> UpdateMerchantCommissionRateAsync(Guid id, object request,
        CancellationToken ct = default)
        => httpClient.PatchAsJsonAsync($"/merchant-commissions/{id}/rate", request, ct);

    public Task<HttpResponseMessage> GetBankCommissionsAsync(Guid? bankId, CancellationToken ct = default)
    {
        var url = bankId.HasValue ? $"/bank-commissions?bankId={bankId}" : "/bank-commissions";
        return httpClient.GetAsync(url, ct);
    }
}