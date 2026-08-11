using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.MerchantCommissions;

/// <summary>
/// Merchant komisyonlarının SALT-OKUMA listesi (enriched): oran + banka aralığı (min–max) +
/// tavan-altı işareti (read-time) + teklif durumu (019 US5). Düzenleme/Finalize YOK (FR-013):
/// komisyon yalnız teklif kabulüyle oluşur; pazarlık Merchant.Agent metin kanalından yürür.
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

    /// <summary>019 US5 — son teklifin durumu (yok / beklemede / kabul / ret + gerekçe + zaman).</summary>
    public CommissionProposalStatusModel? ProposalStatus { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadMerchantsAsync(ct);
        if (MerchantId is { } id && id != Guid.Empty)
        {
            await LoadRowsAsync(id, ct);
            await LoadProposalStatusAsync(id, ct);
        }
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

        // Yalnız merchant oranı oluşmuş satırlar (salt-görünüm). Oranlar teklif kabulüyle doğar.
        Rows = (result.Data?.Items ?? new())
            .Where(i => !i.IsMissing)
            .OrderBy(i => i.Criteria.CardBrand)
            .ThenBy(i => i.Criteria.CardType)
            .ThenBy(i => i.Criteria.TransactionRegion)
            .ThenBy(i => i.Criteria.InstallmentCount)
            .ToList();
    }

    private async Task LoadProposalStatusAsync(Guid merchantId, CancellationToken ct)
    {
        var result = await _commissionApi.GetCommissionProposalStatusAsync(merchantId, ct);
        if (result.IsSuccess)
            ProposalStatus = result.Data;
        else
            AddErrors(result.Messages);
    }
}
