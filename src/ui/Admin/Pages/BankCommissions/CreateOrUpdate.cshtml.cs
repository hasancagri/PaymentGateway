using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.BankCommissions;

/// <summary>
/// Komisyon grid'i: banka seçilince o bankanın taksitlerine göre marka × tip × bölge × taksit
/// tüm kombinasyonları satır olur; dolu/eksik işaretlenir; doldurulanlar tek işlemde upsert edilir.
/// Grid eksenleri domain enum'larından gelir (GET /bank-commissions/criteria-options) — UI kopyalamaz.
/// </summary>
public class CreateOrUpdateModel : BasePageModel
{
    private readonly ICommissionApiClient _api;

    public CreateOrUpdateModel(ICommissionApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public string? BankCode { get; set; }

    /// <summary>Dropdown için aktif bankalar.</summary>
    public List<BankListItem> Banks { get; private set; } = new();

    /// <summary>Seçili banka yüklenip grid kurulduysa true.</summary>
    public bool GridReady { get; private set; }

    [BindProperty] public List<GridCell> Cells { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadBanksAsync(ct);

        // İlk açılışta banka seçili gelsin: parametre yoksa listedeki ilk banka ile grid kurulur.
        if (string.IsNullOrWhiteSpace(BankCode))
            BankCode = Banks.FirstOrDefault()?.Code;

        if (!string.IsNullOrWhiteSpace(BankCode))
            await BuildGridAsync(BankCode, ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadBanksAsync(ct);

        var items = Cells
            .Where(c => c.Rate.HasValue)
            .Select(c => new BulkBankCommissionItem(
                new CriteriaDto
                {
                    CardBrand = c.CardBrand,
                    CardType = c.CardType,
                    TransactionRegion = c.TransactionRegion,
                    InstallmentCount = c.InstallmentCount
                },
                c.Rate!.Value))
            .ToList();

        if (items.Count == 0)
        {
            Flash = "Doldurulan hücre yok; değişiklik kaydedilmedi.";
            return RedirectToPage("CreateOrUpdate", new { bankCode = BankCode });
        }

        var result = await _api.BulkUpsertBankCommissionsAsync(new BulkBankCommissionsRequest(BankCode!, items), ct);
        if (result.IsSuccess)
        {
            Flash = $"Kaydedildi: {result.Data?.Created ?? 0} eklendi, {result.Data?.Updated ?? 0} güncellendi.";
            return RedirectToPage("CreateOrUpdate", new { bankCode = BankCode });
        }

        AddErrors(result.Messages);
        // Hata: grid'i mevcut girdilerle yeniden kur, kullanıcı düzeltsin.
        if (!string.IsNullOrWhiteSpace(BankCode))
            await BuildGridAsync(BankCode, ct);
        return Page();
    }

    private async Task LoadBanksAsync(CancellationToken ct)
    {
        var result = await _api.GetBanksAsync(includeInactive: false, ct);
        if (result.IsSuccess)
            Banks = result.Data?.Items ?? new();
        else
            AddErrors(result.Messages);
    }

    private async Task BuildGridAsync(string bankCode, CancellationToken ct)
    {
        var bankResult = await _api.GetBankAsync(bankCode, ct);
        if (!bankResult.IsSuccess || bankResult.Data is null)
        {
            AddErrors(bankResult.Messages);
            return;
        }

        var optionsResult = await _api.GetCriteriaOptionsAsync(ct);
        if (!optionsResult.IsSuccess || optionsResult.Data is null)
        {
            AddErrors(optionsResult.Messages);
            return;
        }

        var options = optionsResult.Data;

        var existingResult = await _api.GetBankCommissionsAsync(bankCode, ct);
        var existing = existingResult.IsSuccess ? existingResult.Data?.Items ?? new() : new();

        decimal? RateOf(string brand, string type, string region, int inst) =>
            existing.FirstOrDefault(e =>
                e.Criteria.CardBrand == brand &&
                e.Criteria.CardType == type &&
                e.Criteria.TransactionRegion == region &&
                e.Criteria.InstallmentCount == inst)?.Rate;

        var cells = new List<GridCell>();
        foreach (var brand in options.CardBrands)
        foreach (var type in options.CardTypes)
        foreach (var region in options.TransactionRegions)
        foreach (var inst in bankResult.Data.SupportedInstallments)
        {
            var rate = RateOf(brand, type, region, inst);
            cells.Add(new GridCell
            {
                CardBrand = brand,
                CardType = type,
                TransactionRegion = region,
                InstallmentCount = inst,
                Rate = rate,
                Existing = rate.HasValue
            });
        }

        Cells = cells;
        GridReady = true;
    }

    public class GridCell
    {
        public string CardBrand { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string TransactionRegion { get; set; } = string.Empty;
        public int InstallmentCount { get; set; }
        public decimal? Rate { get; set; }
        public bool Existing { get; set; }
    }
}