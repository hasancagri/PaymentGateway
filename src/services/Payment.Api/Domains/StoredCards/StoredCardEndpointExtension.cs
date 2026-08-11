using Payment.Api.Domains.StoredCards.Features.Commands;

namespace Payment.Api.Domains.StoredCards;

/// <summary>
/// Payment.Api'nin ilk merchant-scoped uç grubu: <c>.../merchants/{merchantId}/vault/cards</c>.
/// Tüm uçlar scope <c>cards.write</c> (capability) + policy <c>MerchantScoped</c> ile korunur —
/// her slice kendi endpoint'inde açıkça beyan eder.
/// </summary>
public static class StoredCardEndpointExtension
{
    public static void AddStoredCardGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchants/{merchantId:guid}/vault/cards")
            .WithTags("vault-cards")
            .WithApiVersionSet(apiVersionSet)
            .TokenizeCardGroupItemEndpoint()
            .UpdateCardGroupItemEndpoint()
            .RevokeCardGroupItemEndpoint();
    }
}
