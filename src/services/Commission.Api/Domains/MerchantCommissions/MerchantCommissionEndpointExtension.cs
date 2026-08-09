
namespace Commission.Api.Domains.MerchantCommissions;

public static class MerchantCommissionEndpointExtension
{
    public static void AddMerchantCommissionGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchant-commissions").WithTags("merchant-commissions")
            .WithApiVersionSet(apiVersionSet)
            .CreateMerchantCommissionGroupItemEndpoint()
            .UpdateMerchantCommissionGroupItemEndpoint()
            .BulkUpsertMerchantCommissionsGroupItemEndpoint()
            .GetMerchantCommissionsGroupItemEndpoint()
            .FinalizeMerchantCommissionGridGroupItemEndpoint();
    }
}