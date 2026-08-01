using Admin.Clients;
using Admin.PageModels;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Pages.SettlementAccounts;

/// <summary>
/// Settlement hesabı düzenleme + aktif/pasif. Hesap tenant filtreli yüklenir (başka merchant'ın
/// accountId'si → bulunamadı, sızıntı yok). Kaydet = PUT; durum = ayrı aksiyon (PUT status). Silme
/// yok (soft). Banks/Edit deseni.
/// </summary>
public class EditModel : BasePageModel
{
    private readonly ISettlementAccountApiClient _settlementApi;
    private readonly ICommissionApiClient _commissionApi;

    public EditModel(ISettlementAccountApiClient settlementApi, ICommissionApiClient commissionApi)
    {
        _settlementApi = settlementApi;
        _commissionApi = commissionApi;
    }

    [BindProperty(SupportsGet = true)] public Guid MerchantId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid AccountId { get; set; }

    [BindProperty] public SettlementAccountInput Input { get; set; } = new();

    /// <summary>Banka seçimi kaynağı (Commission katalogunun tamamı).</summary>
    public List<BankCatalogItem> Banks { get; private set; } = new();

    /// <summary>Mevcut durum (Active/Passive); durum butonu metnini belirler.</summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>Hesap bulunup yüklendiyse true; değilse "bulunamadı" gösterilir.</summary>
    public bool Loaded { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _settlementApi.GetAccountAsync(MerchantId, AccountId, ct);
        if (!result.IsSuccess || result.Data is null)
        {
            AddErrors(result.Messages);
            return Page();
        }

        var a = result.Data;
        Input = new SettlementAccountInput
        {
            BankCode = a.BankCode,
            Iban = a.Iban,
            AccountOwnerName = a.AccountOwnerName,
            AccountNo = a.AccountNo,
            AccountDescription = a.AccountDescription
        };
        Status = a.Status;
        Loaded = true;
        await LoadBanksAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var request = new UpdateSettlementAccountRequest(
            Input.BankCode, Input.Iban, Input.AccountOwnerName, Input.AccountNo, Input.AccountDescription);

        var result = await _settlementApi.UpdateAsync(MerchantId, AccountId, request, ct);
        if (result.IsSuccess)
        {
            Flash = "Hesap güncellendi.";
            return RedirectToPage("Index", new { merchantId = MerchantId });
        }

        AddErrors(result.Messages);
        await ReloadStatusAndBanksAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(bool isActive, CancellationToken ct)
    {
        // Buton mevcut duruma göre gönderir; isActive = yeni istenen durum.
        var result = await _settlementApi.SetStatusAsync(
            MerchantId, AccountId, new SetSettlementAccountStatusRequest(isActive), ct);

        if (result.IsSuccess)
        {
            Flash = isActive ? "Hesap aktif edildi." : "Hesap pasife alındı.";
            return RedirectToPage("Edit", new { merchantId = MerchantId, accountId = AccountId });
        }

        AddErrors(result.Messages);
        await ReloadStatusAndBanksAsync(ct);
        return Page();
    }

    private async Task ReloadStatusAndBanksAsync(CancellationToken ct)
    {
        var reload = await _settlementApi.GetAccountAsync(MerchantId, AccountId, ct);
        if (reload.IsSuccess && reload.Data is not null)
        {
            Status = reload.Data.Status;
            Loaded = true;
        }
        await LoadBanksAsync(ct);
    }

    private async Task LoadBanksAsync(CancellationToken ct)
    {
        var result = await _commissionApi.GetBankCatalogAsync(onlyAvailable: false, ct);
        if (result.IsSuccess)
            Banks = result.Data?.Items ?? new();
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