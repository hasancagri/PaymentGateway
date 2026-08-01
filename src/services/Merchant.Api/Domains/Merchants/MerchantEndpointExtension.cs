using Merchant.Api.Domains.Merchants.Features.Commands;
using Merchant.Api.Domains.Merchants.Features.Queries;

namespace Merchant.Api.Domains.Merchants;

public static class MerchantEndpointExtension
{
    public static void AddMerchantGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchants").WithTags("merchants").WithApiVersionSet(apiVersionSet)
            .CreateMerchantGroupItemEndpoint()
            .GetMerchantGroupItemEndpoint()
            .GetMerchantByKeyGroupItemEndpoint()
            .GetAllMerchantsGroupItemEndpoint();
    }
}