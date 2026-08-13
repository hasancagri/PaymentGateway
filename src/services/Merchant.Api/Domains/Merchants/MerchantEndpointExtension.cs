namespace Merchant.Api.Domains.Merchants;

public static class MerchantEndpointExtension
{
    public static void AddMerchantGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchants").WithTags("merchants").WithApiVersionSet(apiVersionSet)
            .CreateMerchantGroupItemEndpoint()
            .UpdateMerchantGroupItemEndpoint()
            .ChangeMerchantStatusGroupItemEndpoint()
            .GetMerchantGroupItemEndpoint()
            .ListMerchantsGroupItemEndpoint();
    }
}