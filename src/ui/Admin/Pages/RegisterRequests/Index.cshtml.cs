using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.RegisterRequests;

/// <summary>
/// 029 US2 — "Merchant Talepleri": tüm başvuruları statüsüyle listeler (tarihçe korunur);
/// Onayla/Reddet yalnız Pending satırlarda. Onay → merchant Active doğar + Identity istemci
/// senkronu (backend). UI kural sızdırmaz, yalnız API sonucunu gösterir.
/// </summary>
public class IndexModel : BasePageModel
{
    private readonly IRegisterRequestApiClient _api;

    public IndexModel(IRegisterRequestApiClient api) => _api = api;

    public List<RegisterRequestItem> Requests { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct)
    {
        var result = await _api.ApproveAsync(id, ct);
        if (result.IsSuccess && result.Data is not null)
        {
            Flash = $"Başvuru onaylandı; merchant oluşturuldu (Id: {result.Data.MerchantId}).";
            return RedirectToPage();
        }

        AddErrors(result.Messages);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string? reason, CancellationToken ct)
    {
        var result = await _api.RejectAsync(id, reason ?? string.Empty, ct);
        if (result.IsSuccess)
        {
            Flash = "Başvuru reddedildi.";
            return RedirectToPage();
        }

        AddErrors(result.Messages);
        return await ReloadAsync(ct);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var result = await _api.GetAllAsync(ct);
        if (result.IsSuccess)
            Requests = result.Data?.Requests ?? new();
        else
            AddErrors(result.Messages);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return Page();
    }
}
