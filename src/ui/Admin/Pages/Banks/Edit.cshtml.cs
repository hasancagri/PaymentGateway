using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.Banks;

public class EditModel : BasePageModel
{
    private readonly ICommissionApiClient _api;

    public EditModel(ICommissionApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public string Code { get; set; } = string.Empty;

    /// <summary>Salt-görünüm banka adı (katalogdan, değiştirilemez).</summary>
    public string BankName { get; private set; } = string.Empty;

    [BindProperty] public BankInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetBankAsync(Code, ct);
        if (!result.IsSuccess || result.Data is null)
        {
            AddErrors(result.Messages);
            return Page();
        }

        BankName = result.Data.Name;
        Input = new BankInput
        {
            IsActive = result.Data.IsActive,
            Installments = result.Data.SupportedInstallments
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var request = new UpdateBankRequest(Input.IsActive, Input.Installments ?? new List<int>());

        var result = await _api.UpdateBankAsync(Code, request, ct);
        if (result.IsSuccess)
        {
            Flash = "Banka güncellendi.";
            return RedirectToPage("Index");
        }

        AddErrors(result.Messages);
        // Ad'ı salt-görünüm için yeniden yükle.
        var reload = await _api.GetBankAsync(Code, ct);
        BankName = reload.Data?.Name ?? Code;
        return Page();
    }

    public class BankInput
    {
        public bool IsActive { get; set; } = true;
        public List<int> Installments { get; set; } = new();
    }
}