using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.Banks;

public class DeleteModel : BasePageModel
{
    private readonly ICommissionApiClient _api;

    public DeleteModel(ICommissionApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public string Code { get; set; } = string.Empty;

    public BankDetail? Bank { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetBankAsync(Code, ct);
        if (!result.IsSuccess)
        {
            AddErrors(result.Messages);
            return Page();
        }

        Bank = result.Data;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var result = await _api.DeleteBankAsync(Code, ct);
        if (result.IsSuccess)
        {
            Flash = "Banka silindi.";
            return RedirectToPage("Index");
        }

        AddErrors(result.Messages);
        // Hata (ör. bağlı komisyon) durumunda onay ekranını tekrar göster.
        var reload = await _api.GetBankAsync(Code, ct);
        Bank = reload.Data;
        return Page();
    }
}