namespace Merchant.Api.Domains.Merchants;

public static class MerchantEndpointExtension
{
    public static void AddMerchantGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        // 044: admin CRUD REST söküldü (MCP muadilleri canlı). Kalanlar: MerchantScoped tekil okuma
        // + hassas-veri BFF çifti (AdminPlaneOnly).
        app.MapGroup("api/v{version:apiVersion}/merchants").WithTags("merchants").WithApiVersionSet(apiVersionSet)
            .GetMerchantGroupItemEndpoint()
            .GetMerchantSensitiveGroupItemEndpoint()
            .UpdateMerchantSensitiveGroupItemEndpoint();
    }
}