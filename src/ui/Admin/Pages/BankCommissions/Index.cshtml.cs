using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.BankCommissions;

public class IndexModel : BasePageModel
{
    private readonly ICommissionApiClient _api;

    public IndexModel(ICommissionApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public string? BankCode { get; set; }

    public List<BankCommissionItem> Items { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetBankCommissionsAsync(BankCode, ct);
        if (result.IsSuccess)
            Items = result.Data?.Items ?? new();
        else
            AddErrors(result.Messages);
    }
}