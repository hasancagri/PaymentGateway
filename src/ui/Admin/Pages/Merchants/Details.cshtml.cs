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

    public async Task<IActionResult> OnPostFinalizeAsync(Guid id, CancellationToken ct)
    {
        var result = await _api.FinalizeAsync(id, ct);
        if (result.IsSuccess && result.Data is not null)
        {
            var r = result.Data;
            if (r.Activated)
                Flash = "Merchant aktive edildi.";
            else
            {
                var eksik = new List<string>();
                if (!r.HasSettlementAccount) eksik.Add("settlement hesabı");
                if (!r.CommissionGridReady) eksik.Add("komisyon grid");
                if (!r.HasReturnUrl) eksik.Add("ReturnUrl");
                Flash = eksik.Count > 0
                    ? $"Aktive edilemedi; eksik: {string.Join(", ", eksik)}."
                    : $"Aktive edilemedi; mevcut durum: {r.Status}.";
            }
        }
        else
            AddErrors(result.Messages);

        return RedirectToPage(new { id });
    }
}