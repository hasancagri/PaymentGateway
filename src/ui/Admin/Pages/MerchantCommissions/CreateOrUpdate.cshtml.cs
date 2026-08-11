using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.MerchantCommissions;

/// <summary>
/// Merchant komisyon grid'i: merchant seçilince marka × tip × bölge × taksit (1..15) tüm
/// kombinasyonları satır olur; dolu/eksik işaretlenir; her satırda banka aralığı (min–max) +
/// tavan-altı işareti (read-time) gösterilir; doldurulanlar tek atomik işlemde upsert edilir.
/// Banka-bağımsız (merchant kombinasyon-bazlı). Grid eksenleri domain enum'larından gelir
/// (GET /bank-commissions/criteria-options) — UI kopyalamaz. Merchant listesi Merchant.Api'den.
/// </summary>
public class CreateOrUpdateModel : BasePageModel
{
    // Merchant banka-bağımsız → taksit ekseni sabit 1..15 (FR-018).
    private static readonly int[] Installments = Enumerable.Range(1, 15).ToArray();

    private readonly IMerchantApiClient _merchantApi;
    private readonly ICommissionApiClient _commissionApi;

    public CreateOrUpdateModel(IMerchantApiClient merchantApi, ICommissionApiClient commissionApi)
    {
        _merchantApi = merchantApi;
        _commissionApi = commissionApi;
    }

    [BindProperty(SupportsGet = true)] public Guid? MerchantId { get; set; }

    /// <summary>Dropdown için merchant'lar.</summary>
    public List<MerchantListItem> Merchants { get; private set; } = new();

    /// <summary>Seçili merchant yüklenip grid kurulduysa true.</summary>
    public bool GridReady { get; private set; }

    [BindProperty] public List<GridCell> Cells { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadMerchantsAsync(ct);

        // İlk açılışta merchant seçili gelsin: parametre yoksa listedeki ilk merchant ile grid kurulur.
        if (MerchantId is null || MerchantId == Guid.Empty)
            MerchantId = Merchants.FirstOrDefault()?.Id;

        if (MerchantId is { } id && id != Guid.Empty)
            await BuildGridAsync(id, ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadMerchantsAsync(ct);

        if (MerchantId is not { } merchantId || merchantId == Guid.Empty)
        {
            Flash = "Merchant seçilmedi.";
            return RedirectToPage("CreateOrUpdate");
        }

        var items = Cells
            .Where(c => c.Rate.HasValue)
            .Select(c => new MerchantCommissionBulkItem(
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
            return RedirectToPage("CreateOrUpdate", new { merchantId });
        }

        var result = await _commissionApi.BulkUpsertMerchantCommissionsAsync(
            new BulkUpsertMerchantCommissionsRequest(merchantId, items), ct);
        if (result.IsSuccess)
        {
            Flash = $"Kaydedildi: {result.Data?.Created ?? 0} eklendi, {result.Data?.Updated ?? 0} güncellendi.";
            return RedirectToPage("CreateOrUpdate", new { merchantId });
        }

        AddErrors(result.Messages);
        // Hata: grid'i mevcut girdilerle yeniden kur, kullanıcı düzeltsin.
        await BuildGridAsync(merchantId, ct);
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

    private async Task BuildGridAsync(Guid merchantId, CancellationToken ct)
    {
        var optionsResult = await _commissionApi.GetCriteriaOptionsAsync(ct);
        if (!optionsResult.IsSuccess || optionsResult.Data is null)
        {
            AddErrors(optionsResult.Messages);
            return;
        }

        var options = optionsResult.Data;

        // Enriched satırlar (merchant oranı + banka aralığı + tavan-altı). Kriterle indexle.
        var enrichedResult = await _commissionApi.GetMerchantCommissionsAsync(merchantId, ct);
        var enriched = enrichedResult.IsSuccess ? enrichedResult.Data?.Items ?? new() : new();

        MerchantCommissionItem? Match(string brand, string type, string region, int inst) =>
            enriched.FirstOrDefault(e =>
                e.Criteria.CardBrand == brand &&
                e.Criteria.CardType == type &&
                e.Criteria.TransactionRegion == region &&
                e.Criteria.InstallmentCount == inst);

        var cells = new List<GridCell>();
        foreach (var brand in options.CardBrands)
        foreach (var type in options.CardTypes)
        foreach (var region in options.TransactionRegions)
        foreach (var inst in Installments)
        {
            var m = Match(brand, type, region, inst);
            cells.Add(new GridCell
            {
                CardBrand = brand,
                CardType = type,
                TransactionRegion = region,
                InstallmentCount = inst,
                Rate = m?.Rate,
                BankMin = m?.BankMin,
                BankMax = m?.BankMax,
                BelowBankCeiling = m?.BelowBankCeiling ?? false,
                Existing = m?.Rate.HasValue ?? false
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
        public decimal? BankMin { get; set; }
        public decimal? BankMax { get; set; }
        public bool BelowBankCeiling { get; set; }
        public bool Existing { get; set; }
    }
}