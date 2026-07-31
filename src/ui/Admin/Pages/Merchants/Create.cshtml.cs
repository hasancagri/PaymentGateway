using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.Merchants;

public class CreateModel : BasePageModel
{
    private readonly IMerchantApiClient _api;

    public CreateModel(IMerchantApiClient api) => _api = api;

    [BindProperty] public CreateMerchantInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var request = new CreateMerchantRequest(
            Input.Name, Input.Email, Input.Phone,
            Input.CountryCode, Input.CityCode, Input.Mcc, Input.WebhookUrl);

        var result = await _api.CreateAsync(request, ct);
        if (result.IsSuccess)
        {
            Flash = "Merchant oluşturuldu.";
            return RedirectToPage("Details", new { id = result.Data!.Id });
        }

        AddErrors(result.Messages);
        return Page();
    }

    public class CreateMerchantInput
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CountryCode { get; set; } = "TR";
        public string CityCode { get; set; } = string.Empty;
        public string Mcc { get; set; } = string.Empty;
        public string WebhookUrl { get; set; } = string.Empty;
    }
}