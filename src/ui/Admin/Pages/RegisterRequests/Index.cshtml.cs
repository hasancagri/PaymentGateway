using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.RegisterRequests;

/// <summary>
/// US2 — "Merchant Talepleri": tüm başvuruları statüsüyle listeler (tarihçe korunur — başvuru anı
/// görünür kalır); onayla/reddet yalnız Pending satırlarda. Onay → merchant Provisioning statüsünde
/// doğar + aktivasyon maili (backend). UI yalnız API sonucunu gösterir.
/// </summary>
public class IndexModel : BasePageModel
{
    private readonly IRegisterRequestApiClient _api;

    public IndexModel(IRegisterRequestApiClient api) => _api = api;

    public List<RegisterRequestListItem> Requests { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostApproveAsync(Guid id, string? note, CancellationToken ct)
    {
        var result = await _api.ApproveAsync(id, note, ct);
        if (result.IsSuccess)
            Flash = "Başvuru onaylandı; merchant oluşturuldu ve aktivasyon maili gönderildi.";
        else
            AddErrors(result.Messages);

        return result.IsSuccess ? RedirectToPage() : await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string? note, CancellationToken ct)
    {
        var result = await _api.RejectAsync(id, note, ct);
        if (result.IsSuccess)
            Flash = "Başvuru reddedildi.";
        else
            AddErrors(result.Messages);

        return result.IsSuccess ? RedirectToPage() : await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var result = await _api.GetAllAsync(ct);
        if (result.IsSuccess)
            Requests = result.Data?.Items ?? new();
        else
            AddErrors(result.Messages);
    }
}
