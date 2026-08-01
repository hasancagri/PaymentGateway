using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.Banks;

public class IndexModel : BasePageModel
{
    private readonly ICommissionApiClient _api;

    public IndexModel(ICommissionApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public bool IncludeInactive { get; set; }

    public List<BankListItem> Items { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetBanksAsync(IncludeInactive, ct);
        if (result.IsSuccess)
            Items = result.Data?.Items ?? new();
        else
            AddErrors(result.Messages);
    }
}