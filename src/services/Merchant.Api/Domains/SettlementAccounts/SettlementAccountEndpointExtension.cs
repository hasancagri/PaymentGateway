using Merchant.Api.Domains.SettlementAccounts.Features.Commands;
using Merchant.Api.Domains.SettlementAccounts.Features.Queries;

namespace Merchant.Api.Domains.SettlementAccounts;

public static class SettlementAccountEndpointExtension
{
    /// <summary>
    /// Settlement hesabı endpoint grubu: <c>merchants/{merchantId}/settlement-accounts</c>.
    /// Tenant sınırı rota <c>{merchantId}</c> ile. Zincir hikâye dilimleriyle genişler.
    /// </summary>
    public static void AddSettlementAccountGroupEndpointExtension(
        this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchants/{merchantId:guid}/settlement-accounts")
            .WithTags("settlement-accounts")
            .WithApiVersionSet(apiVersionSet)
            .CreateSettlementAccountGroupItemEndpoint()
            .GetSettlementAccountsGroupItemEndpoint()
            .GetSettlementAccountGroupItemEndpoint()
            .UpdateSettlementAccountGroupItemEndpoint()
            .SetSettlementAccountStatusGroupItemEndpoint();
    }
}