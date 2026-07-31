using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.BankCommissions;

public class CreateModel : BasePageModel
{
    private readonly ICommissionApiClient _api;

    public CreateModel(ICommissionApiClient api) => _api = api;

    [BindProperty] public BankCommissionInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var request = new CreateBankCommissionRequest(
            Input.BankCode,
            new CriteriaDto
            {
                CardBrand = Input.CardBrand,
                CardType = Input.CardType,
                TransactionRegion = Input.TransactionRegion,
                InstallmentCount = Input.InstallmentCount
            },
            Input.Rate);

        var result = await _api.CreateBankCommissionAsync(request, ct);
        if (result.IsSuccess)
        {
            Flash = "Banka komisyonu eklendi.";
            return RedirectToPage("Index");
        }

        AddErrors(result.Messages);
        return Page();
    }

    public class BankCommissionInput
    {
        public string BankCode { get; set; } = string.Empty;
        public string CardBrand { get; set; } = "VISA";
        public string CardType { get; set; } = "CREDIT";
        public string TransactionRegion { get; set; } = "DOMESTIC";
        public int InstallmentCount { get; set; } = 1;
        public decimal Rate { get; set; }
    }
}