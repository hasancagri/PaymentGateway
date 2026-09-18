using Commission.Api.Domains.CommissionPolicies.Features.Queries;

namespace Commission.Api.Domains.CommissionPolicies;

public static class CommissionPolicyEndpointExtension
{
    public static void AddCommissionPolicyGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        // 044: admin REST uçları söküldü (create/margin/status MCP tool'larında; liste/calculate
        // tüketicisiz — R2). Kalan tek uç: MerchantScoped tekil politika okuması.
        app.MapGroup("api/v{version:apiVersion}/commission-policies")
            .WithTags("commission-policies")
            .WithApiVersionSet(apiVersionSet)
            .GetCommissionPolicyGroupItemEndpoint();
    }
}