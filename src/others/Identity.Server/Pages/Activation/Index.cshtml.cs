using Identity.Server.Activation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Identity.Server.Pages.Activation;

/// <summary>
/// US3 — aktivasyon sayfası (013 D4). GET: token'ı gizli alanda formu gösterir. POST: Merchant.Api
/// redeem'i senkron çağırır → MerchantKey'i <b>bir kez</b> render eder ("bir daha gösterilmez").
/// Sayfa key custody tutmaz; yalnız redeem yanıtını gösterir. İkinci redeem/süre dolmuş → hata.
/// </summary>
public class IndexModel(MerchantActivationClient client) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public bool Redeemed { get; private set; }
    public string? MerchantKey { get; private set; }
    public Guid MerchantId { get; private set; }
    public string? Error { get; private set; }

    public void OnGet()
    {
        if (string.IsNullOrWhiteSpace(Token))
            Error = "Aktivasyon token'ı eksik.";
    }

    public async Task OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            Error = "Aktivasyon token'ı eksik.";
            return;
        }

        var result = await client.RedeemAsync(Token, ct);
        if (result.Success)
        {
            Redeemed = true;
            MerchantId = result.MerchantId;
            MerchantKey = result.MerchantKey;
        }
        else
        {
            Error = result.Error ?? "Aktivasyon başarısız.";
        }
    }
}
