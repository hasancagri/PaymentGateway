using Merchant.Api.Domains.MerchantSettlementAccounts.Features.Commands;
using Merchant.Api.Domains.MerchantSettlementAccounts.Features.Queries;

namespace Merchant.Api.Domains.MerchantSettlementAccounts;

public static class MerchantSettlementAccountEndpointExtension
{
    /// <summary>
    /// Settlement hesabı endpoint grubu: <c>merchants/{merchantId}/settlement-accounts</c>.
    /// Tenant sınırı rota <c>{merchantId}</c> ile. Zincir hikâye dilimleriyle genişler.
    /// </summary>
    public static void AddMerchantSettlementAccountGroupEndpointExtension(
        this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchants/{merchantId:guid}/settlement-accounts")
            .WithTags("settlement-accounts")
            .WithApiVersionSet(apiVersionSet)
            .CreateSettlementAccountGroupItemEndpoint()
            .GetMerchantSettlementAccountsGroupItemEndpoint()
            .GetSettlementAccountGroupItemEndpoint()
            .UpdateSettlementAccountGroupItemEndpoint()
            .SetSettlementAccountStatusGroupItemEndpoint();
    }
}