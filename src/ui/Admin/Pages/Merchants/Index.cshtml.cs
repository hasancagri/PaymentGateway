using Admin.Clients;
using Admin.PageModels;

namespace Admin.Pages.Merchants;

public class IndexModel : BasePageModel
{
    private readonly IMerchantApiClient _api;

    public IndexModel(IMerchantApiClient api) => _api = api;

    public List<MerchantListItem> Merchants { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetAllAsync(ct);
        if (result.IsSuccess)
            Merchants = result.Data?.Merchants ?? new();
        else
            AddErrors(result.Messages);
    }
}