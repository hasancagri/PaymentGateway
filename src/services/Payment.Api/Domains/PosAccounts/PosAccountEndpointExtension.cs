using Payment.Api.Domains.PosAccounts.Features.Commands;

namespace Payment.Api.Domains.PosAccounts;

public static class PosAccountEndpointExtension
{
    public static void AddPosAccountGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/pos-accounts").WithTags("pos-accounts").WithApiVersionSet(apiVersionSet)
            .CreatePosAccountGroupItemEndpoint()
            .UpdatePosAccountGroupItemEndpoint()
            .GetAllPosAccountsGroupItemEndpoint();
    }
}