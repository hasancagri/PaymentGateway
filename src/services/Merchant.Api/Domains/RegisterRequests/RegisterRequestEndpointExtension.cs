using Merchant.Api.Domains.RegisterRequests.Features.Commands;
using Merchant.Api.Domains.RegisterRequests.Features.Queries;

namespace Merchant.Api.Domains.RegisterRequests;

public static class RegisterRequestEndpointExtension
{
    public static void AddRegisterRequestGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/register-requests").WithTags("register-requests")
            .WithApiVersionSet(apiVersionSet)
            .ListRegisterRequestsGroupItemEndpoint()
            .ApproveRegisterRequestGroupItemEndpoint()
            .RejectRegisterRequestGroupItemEndpoint();
    }
}
