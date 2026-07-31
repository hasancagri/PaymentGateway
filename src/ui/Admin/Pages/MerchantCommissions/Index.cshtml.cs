using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.MerchantCommissions;

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
    [BindProperty(SupportsGet = true)] public string? BankCode { get; set; }

    public List<MerchantListItem> Merchants { get; private set; } = new();
    public List<Row> Rows { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadMerchantsAsync(ct);
        if (MerchantId is { } id && id != Guid.Empty)
            await LoadRowsAsync(id, ct);
    }

    public async Task<IActionResult> OnPostAsync(Guid merchantId, Guid bankCommissionId, decimal rate, CancellationToken ct)
    {
        var result = await _commissionApi.CreateMerchantCommissionAsync(
            new CreateMerchantCommissionRequest(merchantId, bankCommissionId, rate), ct);

        if (result.IsSuccess)
        {
            Flash = "Komisyon kaydedildi.";
            return RedirectToPage(new { merchantId, bankCode = BankCode });
        }

        // Hata (ör. invariant): PRG yerine sayfayı yeniden kur ki mesaj görünsün.
        AddErrors(result.Messages);
        MerchantId = merchantId;
        await LoadMerchantsAsync(ct);
        await LoadRowsAsync(merchantId, ct);
        return Page();
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
        var banks = await _commissionApi.GetBankCommissionsAsync(BankCode, ct);
        if (!banks.IsSuccess)
        {
            AddErrors(banks.Messages);
            return;
        }

        var merch = await _commissionApi.GetMerchantCommissionsAsync(merchantId, ct);
        var existing = merch.IsSuccess
            ? merch.Data!.Items.GroupBy(x => x.BankCommissionId).ToDictionary(g => g.Key, g => g.First().Rate)
            : new Dictionary<Guid, decimal>();

        Rows = banks.Data!.Items.Select(b => new Row
        {
            BankCommissionId = b.Id,
            BankCode = b.BankCode,
            Criteria = b.Criteria,
            BankRate = b.Rate,
            MerchantRate = existing.TryGetValue(b.Id, out var rate) ? rate : null
        }).ToList();
    }

    public class Row
    {
        public Guid BankCommissionId { get; set; }
        public string BankCode { get; set; } = string.Empty;
        public CriteriaDto Criteria { get; set; } = new();
        public decimal BankRate { get; set; }
        public decimal? MerchantRate { get; set; }
    }
}