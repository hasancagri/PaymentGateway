using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.BankCommissions;

public class IndexModel : BasePageModel
{
    private readonly ICommissionApiClient _api;

    public IndexModel(ICommissionApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public string? BankCode { get; set; }

    public List<BankListItem> Banks { get; private set; } = new();

    public List<BankCommissionItem> Items { get; private set; } = new();

    private Dictionary<string, string> _bankNames = new();

    /// <summary>Banka kodunu adına çözer; çözülemezse kodu döndürür.</summary>
    public string BankNameOf(string code) =>
        _bankNames.TryGetValue(code, out var name) ? name : code;

    public async Task OnGetAsync(CancellationToken ct)
    {
        var banksResult = await _api.GetBanksAsync(includeInactive: true, ct);
        if (banksResult.IsSuccess)
        {
            Banks = banksResult.Data?.Items ?? new();
            _bankNames = Banks.ToDictionary(b => b.Code, b => b.Name);
        }

        var result = await _api.GetBankCommissionsAsync(BankCode, ct);
        if (result.IsSuccess)
            Items = result.Data?.Items ?? new();
        else
            AddErrors(result.Messages);
    }
}