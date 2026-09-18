using Merchant.Api.Domains.CredentialRevealLinks.Features.Commands;
using Merchant.Api.Domains.OnboardingFormSessions;

namespace Merchant.Api.Domains.CredentialRevealLinks;

// 045 US2: tek gösterimlik teslim sayfası — ANONİM (token = yetki; mail'deki link). Gösterim
// linki tüketir; ikinci açılış / süre sonu "PG Admin'e başvurun" nötr sayfası.
public static class CredentialRevealLinkEndpointExtension
{
    public static void MapCredentialRevealPage(this WebApplication app)
    {
        app.MapGet("/onboarding/reveal/{token}", async (
            string token, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<RevealCredentials.RevealCredentialsResponse>>(
                new RevealCredentials.RevealCredentialsCommand(token), ct);

            return result.IsSuccess
                ? OnboardingFormSessionEndpointExtension.Html(
                    RevealPage(result.Data!.MerchantId, result.Data.MerchantKey), StatusCodes.Status200OK)
                : OnboardingFormSessionEndpointExtension.Html(DeadLinkPage, StatusCodes.Status404NotFound);
        });
    }

    private static string RevealPage(Guid merchantId, string merchantKey) =>
        OnboardingFormSessionEndpointExtension.Layout("Erişim Bilgileriniz", $"""
        <h1>Erişim bilgileriniz</h1>
        <p>Bu bilgiler <strong>yalnız bu sayfada ve bir kez</strong> gösterilir — sayfayı
        yenilerseniz tekrar göremezsiniz. Şimdi mağazanızın merchant-bilgisi ekranına girin.</p>
        <label>MerchantId</label>
        <div class="secret">{merchantId}</div>
        <label>MerchantKey</label>
        <div class="secret">{merchantKey}</div>
        <p>Bilgileri kaydettikten sonra bu sayfayı kapatın. Kaybederseniz gateway yöneticisinden
        yeni teslim bağlantısı isteyin.</p>
        """);

    private static readonly string DeadLinkPage = OnboardingFormSessionEndpointExtension.Layout(
        "Bağlantı geçersiz", """
        <h1>Bağlantı geçersiz</h1>
        <p>Bu teslim bağlantısı kullanılmış ya da süresi dolmuş. Erişim bilgileriniz için gateway
        yöneticisinden yeni bir teslim bağlantısı isteyin.</p>
        """);
}
