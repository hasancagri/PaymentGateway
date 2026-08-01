using Commission.Api.Domains.Banks.Features.Commands;
using Commission.Api.Domains.Banks.Features.Queries;

namespace Commission.Api.Domains.Banks;

public static class BankEndpointExtension
{
    public static void AddBankGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/banks").WithTags("banks")
            .WithApiVersionSet(apiVersionSet)
            .CreateBankGroupItemEndpoint()
            .GetBankCatalogGroupItemEndpoint()
            .GetBanksGroupItemEndpoint()
            .GetBankGroupItemEndpoint()
            .UpdateBankGroupItemEndpoint()
            .DeleteBankGroupItemEndpoint();
    }
}