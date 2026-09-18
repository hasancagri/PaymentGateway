using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.Merchants;

/// <summary>
/// 044 — tek hassas-veri sayfası: Email, GSM, TCKN, IBAN, vergi no YALNIZ buradan görüntülenir/
/// düzenlenir (MCP sözleşmelerinde bu alanlar yok; agent sohbette bu sayfanın linkini verir).
/// Dar BFF çifti: GET/PUT /merchants/{id}/sensitive (AdminPlaneOnly).
/// </summary>
public class SensitiveModel : BasePageModel
{
    private readonly IMerchantApiClient _api;

    public SensitiveModel(IMerchantApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)]
    public Guid? MerchantId { get; set; }

    public MerchantSensitiveDetail? Merchant { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (MerchantId is not { } id || id == Guid.Empty)
            return;

        var result = await _api.GetSensitiveAsync(id, ct);
        if (result.IsSuccess)
            Merchant = result.Data;
        else
            AddErrors(result.Messages);
    }

    public async Task<IActionResult> OnPostAsync(
        string email, string gsmNumber, string? identityNumber, string iban, string? taxNumber,
        CancellationToken ct)
    {
        if (MerchantId is not { } id || id == Guid.Empty)
            return RedirectToPage();

        var result = await _api.UpdateSensitiveAsync(
            id, new UpdateMerchantSensitiveRequest(email, gsmNumber, identityNumber, iban, taxNumber), ct);
        if (result.IsSuccess)
        {
            Flash = "Hassas veriler güncellendi.";
            return RedirectToPage(new { merchantId = id });
        }

        AddErrors(result.Messages);
        var reload = await _api.GetSensitiveAsync(id, ct);
        if (reload.IsSuccess)
            Merchant = reload.Data;
        return Page();
    }
}