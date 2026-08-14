using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.CommissionPolicies;

/// <summary>
/// 024 (ertelenen Admin UI) — "Komisyon Politikaları": merchant-başına gateway marjı
/// (yüzde + sabit ücret). Liste + oluşturma + satır içi marj güncelleme + durum değişimi.
/// UI kural sızdırmaz; tavanlar (oran 0.20, sabit 100) backend'de doğrulanır.
/// </summary>
public class IndexModel : BasePageModel
{
    private readonly ICommissionPolicyApiClient _api;
    private readonly IMerchantApiClient _merchantApi;

    public IndexModel(ICommissionPolicyApiClient api, IMerchantApiClient merchantApi)
    {
        _api = api;
        _merchantApi = merchantApi;
    }

    public List<CommissionPolicyItem> Policies { get; private set; } = new();

    /// <summary>Oluşturma dropdown'ı + satırlarda ad göstermek için merchant listesi.</summary>
    public List<MerchantListItem> Merchants { get; private set; } = new();

    public string MerchantName(Guid merchantId) =>
        Merchants.FirstOrDefault(m => m.MerchantId == merchantId)?.Name ?? merchantId.ToString();

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(Guid merchantId, decimal ratePercent, decimal fixedFee, CancellationToken ct)
    {
        var result = await _api.CreateAsync(new CreateCommissionPolicyRequest(merchantId, ratePercent, fixedFee), ct);
        if (result.IsSuccess)
        {
            Flash = "Komisyon politikası oluşturuldu.";
            return RedirectToPage();
        }

        AddErrors(result.Messages);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostUpdateMarginAsync(Guid merchantId, decimal ratePercent, decimal fixedFee, CancellationToken ct)
    {
        var result = await _api.UpdateMarginAsync(merchantId, ratePercent, fixedFee, ct);
        if (result.IsSuccess)
        {
            Flash = "Marj güncellendi.";
            return RedirectToPage();
        }

        AddErrors(result.Messages);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostChangeStatusAsync(Guid merchantId, string status, CancellationToken ct)
    {
        var result = await _api.ChangeStatusAsync(merchantId, status, ct);
        if (result.IsSuccess)
        {
            Flash = $"Politika durumu {result.Data?.Status} yapıldı.";
            return RedirectToPage();
        }

        AddErrors(result.Messages);
        return await ReloadAsync(ct);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var policies = await _api.GetAllAsync(ct);
        if (policies.IsSuccess)
            Policies = policies.Data?.Policies ?? new();
        else
            AddErrors(policies.Messages);

        var merchants = await _merchantApi.GetAllAsync(ct);
        if (merchants.IsSuccess)
            Merchants = merchants.Data?.Merchants ?? new();
        else
            AddErrors(merchants.Messages);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return Page();
    }
}