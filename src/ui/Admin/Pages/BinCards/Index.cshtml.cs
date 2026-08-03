using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.BinCards;

public class IndexModel : BasePageModel
{
    private readonly IBinCardApiClient _api;

    public IndexModel(IBinCardApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public string? BankCode { get; set; }
    [BindProperty(SupportsGet = true)] public string? CardProgram { get; set; }
    [BindProperty(SupportsGet = true)] public string? CardType { get; set; }
    [BindProperty(SupportsGet = true)] public string? CardBrand { get; set; }
    [BindProperty(SupportsGet = true)] public bool? Commercial { get; set; }
    // NOT: "Page" Razor Pages'te rezerve route anahtarı (asp-page). Bind edilmez → PageNo kullan.
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;

    public BinCardListResponse Result { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        var filter = new BinCardListFilter
        {
            BankCode = BankCode,
            CardProgram = CardProgram,
            CardType = CardType,
            CardBrand = CardBrand,
            Commercial = Commercial,
            Page = PageNo < 1 ? 1 : PageNo
        };

        var result = await _api.ListAsync(filter, ct);
        if (result.IsSuccess)
            Result = result.Data ?? new BinCardListResponse();
        else
            AddErrors(result.Messages);
    }
}