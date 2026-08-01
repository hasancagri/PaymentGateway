using Commission.Api.Domains.BankCommissions.Features.Commands;
using Commission.Api.Domains.BankCommissions.Features.Queries;

namespace Commission.Api.Domains.BankCommissions;

public static class BankCommissionEndpointExtension
{
    public static void AddBankCommissionGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/bank-commissions").WithTags("bank-commissions")
            .WithApiVersionSet(apiVersionSet)
            .CreateBankCommissionGroupItemEndpoint()
            .BulkUpsertBankCommissionsGroupItemEndpoint()
            .GetCriteriaOptionsGroupItemEndpoint()
            .GetBankCommissionsGroupItemEndpoint();
    }
}