using System.Net.Http.Json;
using System.Web;

namespace Admin.Clients;

/// <summary>
/// Payment.Api BinCard katalog okuma API'si (009). Salt-okuma: tekil detay (<c>GET /api/v1/bin-cards/{bin}</c>)
/// + filtreli sayfalı liste (<c>GET /api/v1/bin-cards?...</c>). BaseAddress service discovery ile
/// <c>http://payment-api</c>. <c>SettlementAccountApiClient</c> deseniyle aynı.
/// </summary>
public interface IBinCardApiClient
{
    Task<ApiResult<BinCardDetail>> GetDetailAsync(string bin, CancellationToken ct = default);
    Task<ApiResult<BinCardListResponse>> ListAsync(BinCardListFilter filter, CancellationToken ct = default);
}

public class BinCardApiClient : ApiClientBase, IBinCardApiClient
{
    public BinCardApiClient(HttpClient http) : base(http)
    {
    }

    public Task<ApiResult<BinCardDetail>> GetDetailAsync(string bin, CancellationToken ct = default) =>
        SendAsync<BinCardDetail>(() => Http.GetAsync($"/api/v1/bin-cards/{Uri.EscapeDataString(bin)}", ct), ct);

    public Task<ApiResult<BinCardListResponse>> ListAsync(BinCardListFilter filter, CancellationToken ct = default) =>
        SendAsync<BinCardListResponse>(() => Http.GetAsync($"/api/v1/bin-cards{BuildQuery(filter)}", ct), ct);

    private static string BuildQuery(BinCardListFilter f)
    {
        var q = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrWhiteSpace(f.BankCode)) q["bankCode"] = f.BankCode;
        if (!string.IsNullOrWhiteSpace(f.CardProgram)) q["cardProgram"] = f.CardProgram;
        if (!string.IsNullOrWhiteSpace(f.CardType)) q["cardType"] = f.CardType;
        if (!string.IsNullOrWhiteSpace(f.CardBrand)) q["cardBrand"] = f.CardBrand;
        if (f.Commercial is not null) q["commercial"] = f.Commercial.Value ? "true" : "false";
        q["page"] = (f.Page < 1 ? 1 : f.Page).ToString();

        var s = q.ToString();
        return string.IsNullOrEmpty(s) ? string.Empty : $"?{s}";
    }
}