using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.Merchants;

public class DetailsModel : BasePageModel
{
    private readonly IMerchantApiClient _api;

    public DetailsModel(IMerchantApiClient api) => _api = api;

    public MerchantDetail? Merchant { get; private set; }

    public async Task OnGetAsync(Guid id, CancellationToken ct)
    {
        var result = await _api.GetAsync(id, ct);
        if (result.IsSuccess)
            Merchant = result.Data;
        else
            AddErrors(result.Messages);
    }
}