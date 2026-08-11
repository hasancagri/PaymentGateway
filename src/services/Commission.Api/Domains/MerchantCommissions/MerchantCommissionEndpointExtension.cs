namespace Commission.Api.Domains.MerchantCommissions;

// 019 (FR-013): Create/Update/BulkUpsert/Finalize uçları SÖKÜLDÜ — merchant komisyonu artık YALNIZ
// teklif kabulünde yazılır (AcceptCommissionProposal). Admin düzlemi salt-okuma.
public static class MerchantCommissionEndpointExtension
{
    public static void AddMerchantCommissionGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchant-commissions").WithTags("merchant-commissions")
            .WithApiVersionSet(apiVersionSet)
            .GetMerchantCommissionsGroupItemEndpoint();
    }
}
