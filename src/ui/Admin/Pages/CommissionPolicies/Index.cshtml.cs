using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.CommissionPolicies;

/// <summary>
/// 024/030 — "Komisyon Politikaları": merchant-başına tutar-kademeli gateway marj tarifesi.
/// Liste + kademe grid'li oluşturma + tarife düzenleme (tablo bütünüyle değişir) + durum değişimi.
/// UI kural sızdırmaz; tablo doğrulaması (0 başlangıç, artan sınırlar, tavanlar, ≤10 kademe)
/// backend'de. Grid JS'siz: 10 sabit satır, boş satırlar post'ta atlanır.
/// </summary>
public class IndexModel : BasePageModel
{
    public const int GridRows = 10;

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

    /// <summary>Düzenleme modu: ?editMerchantId= ile gelinir; grid bu politikanın kademeleriyle dolar.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? EditMerchantId { get; set; }

    public CommissionPolicyItem? EditPolicy =>
        EditMerchantId is { } id ? Policies.FirstOrDefault(p => p.MerchantId == id) : null;

    public string MerchantName(Guid merchantId) =>
        Merchants.FirstOrDefault(m => m.MerchantId == merchantId)?.Name ?? merchantId.ToString();

    /// <summary>Liste kolonu için kompakt tarife metni: "0+: %2,5 + 1 TL · 1.000+: %2 + 1 TL".</summary>
    public static string TariffText(List<TierDto> tiers) =>
        string.Join(" · ", tiers.Select(t =>
            $"{t.FromAmount:0.##}+: %{t.RatePercent * 100:0.##}" +
            (t.FixedFee > 0 ? $" + {t.FixedFee:0.##} TL" : string.Empty)));

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(Guid merchantId, TierInput[] tiers, CancellationToken ct)
    {
        var result = await _api.CreateAsync(new CreateCommissionPolicyRequest(merchantId, ToTierDtos(tiers)), ct);
        if (result.IsSuccess)
        {
            Flash = "Komisyon politikası oluşturuldu.";
            return RedirectToPage();
        }

        AddErrors(result.Messages);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostUpdateMarginAsync(Guid merchantId, TierInput[] tiers, CancellationToken ct)
    {
        var result = await _api.UpdateMarginAsync(merchantId, ToTierDtos(tiers), ct);
        if (result.IsSuccess)
        {
            Flash = "Tarife güncellendi.";
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

    /// <summary>Boş grid satırlarını atlar (üç alan da boşsa satır yok sayılır); sıra korunur.</summary>
    private static List<TierDto> ToTierDtos(TierInput[]? tiers) =>
        (tiers ?? Array.Empty<TierInput>())
        .Where(t => t.FromAmount is not null || t.RatePercent is not null || t.FixedFee is not null)
        .Select(t => new TierDto(t.FromAmount ?? 0m, t.RatePercent ?? 0m, t.FixedFee ?? 0m))
        .ToList();

    public class TierInput
    {
        public decimal? FromAmount { get; set; }
        public decimal? RatePercent { get; set; }
        public decimal? FixedFee { get; set; }
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
