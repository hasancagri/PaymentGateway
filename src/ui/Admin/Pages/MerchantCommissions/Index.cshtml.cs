using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.MerchantCommissions;

/// <summary>
/// Merchant komisyonlarının salt-görünüm listesi (enriched): oran + banka aralığı (min–max) +
/// tavan-altı işareti (read-time). Düzenleme grid'de (Create). Banka kodu filtresi YOK (FR-017).
/// </summary>
public class IndexModel : BasePageModel
{
    private readonly IMerchantApiClient _merchantApi;
    private readonly ICommissionApiClient _commissionApi;

    public IndexModel(IMerchantApiClient merchantApi, ICommissionApiClient commissionApi)
    {
        _merchantApi = merchantApi;
        _commissionApi = commissionApi;
    }

    [BindProperty(SupportsGet = true)] public Guid? MerchantId { get; set; }

    public List<MerchantListItem> Merchants { get; private set; } = new();
    public List<MerchantCommissionItem> Rows { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadMerchantsAsync(ct);
        if (MerchantId is { } id && id != Guid.Empty)
            await LoadRowsAsync(id, ct);
    }

    private async Task LoadMerchantsAsync(CancellationToken ct)
    {
        var result = await _merchantApi.GetAllAsync(ct);
        if (result.IsSuccess)
            Merchants = result.Data?.Merchants ?? new();
        else
            AddErrors(result.Messages);
    }

    private async Task LoadRowsAsync(Guid merchantId, CancellationToken ct)
    {
        var result = await _commissionApi.GetMerchantCommissionsAsync(merchantId, ct);
        if (!result.IsSuccess)
        {
            AddErrors(result.Messages);
            return;
        }

        // Yalnız merchant oranı girilmiş satırlar (salt-görünüm liste). Eksik hücreler grid'de yönetilir.
        Rows = (result.Data?.Items ?? new())
            .Where(i => !i.IsMissing)
            .OrderBy(i => i.Criteria.CardBrand)
            .ThenBy(i => i.Criteria.CardType)
            .ThenBy(i => i.Criteria.TransactionRegion)
            .ThenBy(i => i.Criteria.InstallmentCount)
            .ToList();
    }
}