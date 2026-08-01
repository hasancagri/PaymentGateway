using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.SettlementAccounts;

/// <summary>
/// Bir merchant'ın settlement hesaplarının listesi (yalnız o merchant — tenant sınırı). Merchant
/// dropdown'dan seçilir; boş liste bilgilendirici (hata değil). MerchantCommissions/Index deseni.
/// </summary>
public class IndexModel : BasePageModel
{
    private readonly IMerchantApiClient _merchantApi;
    private readonly ISettlementAccountApiClient _settlementApi;

    public IndexModel(IMerchantApiClient merchantApi, ISettlementAccountApiClient settlementApi)
    {
        _merchantApi = merchantApi;
        _settlementApi = settlementApi;
    }

    [BindProperty(SupportsGet = true)] public Guid? MerchantId { get; set; }

    public List<MerchantListItem> Merchants { get; private set; } = new();
    public List<SettlementAccountListItem> Accounts { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadMerchantsAsync(ct);
        if (MerchantId is { } id && id != Guid.Empty)
            await LoadAccountsAsync(id, ct);
    }

    private async Task LoadMerchantsAsync(CancellationToken ct)
    {
        var result = await _merchantApi.GetAllAsync(ct);
        if (result.IsSuccess)
            Merchants = result.Data?.Merchants ?? new();
        else
            AddErrors(result.Messages);
    }

    private async Task LoadAccountsAsync(Guid merchantId, CancellationToken ct)
    {
        var result = await _settlementApi.GetAccountsAsync(merchantId, ct);
        if (result.IsSuccess)
            Accounts = result.Data?.Accounts ?? new();
        else
            AddErrors(result.Messages);
    }
}