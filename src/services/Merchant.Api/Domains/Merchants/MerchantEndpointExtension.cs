namespace Merchant.Api.Domains.Merchants;

public static class MerchantEndpointExtension
{
    public static void AddMerchantGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        // 044: admin CRUD REST söküldü (MCP muadilleri canlı). 048: hassas-veri BFF çifti de SÖKÜLDÜ —
        // hassas veri artık hosted link'ten (SensitiveEntryEndpointExtension) akar; GetMerchantSensitive/
        // UpdateMerchantSensitive HANDLER'ları KALIR (token-endpoint IMessageBus ile çağırır).
        app.MapGroup("api/v{version:apiVersion}/merchants").WithTags("merchants").WithApiVersionSet(apiVersionSet)
            .GetMerchantGroupItemEndpoint()
            // 046 US2: key yenileme geçmişi (MerchantScoped salt-okuma).
            .GetMerchantKeyReissueHistoryGroupItemEndpoint();
    }
}