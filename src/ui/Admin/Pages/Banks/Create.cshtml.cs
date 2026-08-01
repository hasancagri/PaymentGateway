using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.Banks;

public class CreateModel : BasePageModel
{
    private readonly ICommissionApiClient _api;

    public CreateModel(ICommissionApiClient api) => _api = api;

    [BindProperty] public BankInput Input { get; set; } = new();

    /// <summary>Katalogdan henüz eklenmemiş bankalar (selectbox kaynağı).</summary>
    public List<BankCatalogItem> Catalog { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadCatalogAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var request = new CreateBankRequest(Input.Code, Input.Installments ?? new List<int>());

        var result = await _api.CreateBankAsync(request, ct);
        if (result.IsSuccess)
        {
            Flash = "Banka eklendi.";
            return RedirectToPage("Index");
        }

        AddErrors(result.Messages);
        await LoadCatalogAsync(ct);
        return Page();
    }

    private async Task LoadCatalogAsync(CancellationToken ct)
    {
        var result = await _api.GetBankCatalogAsync(onlyAvailable: true, ct);
        if (result.IsSuccess)
            Catalog = result.Data?.Items ?? new();
        else
            AddErrors(result.Messages);
    }

    public class BankInput
    {
        public string Code { get; set; } = string.Empty;
        public List<int> Installments { get; set; } = new() { 1, 2, 3, 6, 9, 12 };
    }
}