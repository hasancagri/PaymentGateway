using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.SettlementAccounts;

/// <summary>
/// Seçili merchant'a yeni settlement hesabı ekleme formu. Banka dropdown Commission katalogundan
/// (research D1). UI ek doğrulama koymaz; API sonucu gösterilir, hata durumunda form korunur
/// (Banks/Create deseni).
/// </summary>
public class CreateModel : BasePageModel
{
    private readonly ISettlementAccountApiClient _settlementApi;
    private readonly ICommissionApiClient _commissionApi;

    public CreateModel(ISettlementAccountApiClient settlementApi, ICommissionApiClient commissionApi)
    {
        _settlementApi = settlementApi;
        _commissionApi = commissionApi;
    }

    [BindProperty(SupportsGet = true)] public Guid MerchantId { get; set; }

    [BindProperty] public SettlementAccountInput Input { get; set; } = new();

    /// <summary>Banka seçimi kaynağı (Commission katalogunun tamamı).</summary>
    public List<BankCatalogItem> Banks { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadBanksAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var request = new CreateSettlementAccountRequest(
            Input.BankCode, Input.Iban, Input.AccountOwnerName, Input.AccountNo, Input.AccountDescription);

        var result = await _settlementApi.CreateAsync(MerchantId, request, ct);
        if (result.IsSuccess)
        {
            Flash = "Hesap eklendi.";
            return RedirectToPage("Index", new { merchantId = MerchantId });
        }

        AddErrors(result.Messages);
        await LoadBanksAsync(ct);
        return Page();
    }

    private async Task LoadBanksAsync(CancellationToken ct)
    {
        var result = await _commissionApi.GetBankCatalogAsync(onlyAvailable: false, ct);
        if (result.IsSuccess)
            Banks = result.Data?.Items ?? new();
        else
            AddErrors(result.Messages);
    }

    public class SettlementAccountInput
    {
        public string BankCode { get; set; } = string.Empty;
        public string Iban { get; set; } = string.Empty;
        public string AccountOwnerName { get; set; } = string.Empty;
        public string AccountNo { get; set; } = string.Empty;
        public string AccountDescription { get; set; } = string.Empty;
    }
}