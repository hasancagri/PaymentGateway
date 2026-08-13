using Commission.Api.Domains.CommissionPolicies.Features.Commands;
using Commission.Api.Domains.CommissionPolicies.Features.Queries;

namespace Commission.Api.Domains.CommissionPolicies;

public static class CommissionPolicyEndpointExtension
{
    public static void AddCommissionPolicyGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/commission-policies")
            .WithTags("commission-policies")
            .WithApiVersionSet(apiVersionSet)
            .CreateCommissionPolicyGroupItemEndpoint()
            .UpdateCommissionPolicyMarginGroupItemEndpoint()
            .ChangeCommissionPolicyStatusGroupItemEndpoint()
            .CalculateEffectiveCommissionGroupItemEndpoint()
            .GetCommissionPolicyGroupItemEndpoint()
            .ListCommissionPoliciesGroupItemEndpoint();
    }
}
