
namespace Commission.Api.Domains.Banks;

public static class BankEndpointExtension
{
    public static void AddBankGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/banks").WithTags("banks")
            .WithApiVersionSet(apiVersionSet)
            .CreateBankGroupItemEndpoint()
            .GetBanksGroupItemEndpoint()
            .GetBankGroupItemEndpoint()
            .UpdateBankGroupItemEndpoint()
            .DeleteBankGroupItemEndpoint();
    }
}