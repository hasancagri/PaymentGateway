using Commission.Api.Domains.CommissionPolicies;

namespace Commission.Api.Domains.CommissionPolicies.Features.Queries;

// 024: merchant kendi marj politikasını/efektif oranını görür (US3/FR-010). MerchantScoped: yalnız
// kendi {merchantId} (route-claim eşleşmesi, fail-closed). Yoksa NotFound.
public static class GetCommissionPolicy
{
    public record GetCommissionPolicyQuery(Guid MerchantId);

    public class GetCommissionPolicyResponse
    {
        public Guid MerchantId { get; set; }
        public decimal RatePercent { get; set; }
        public decimal FixedFee { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class GetCommissionPolicyQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetCommissionPolicyResponse>> Handle(
            GetCommissionPolicyQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var policy = await session.Query<CommissionPolicy>()
                .Where(p => p.MerchantId == query.MerchantId && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (policy is null)
                return FeatureObjectResultModel<GetCommissionPolicyResponse>.NotFound();

            return FeatureObjectResultModel<GetCommissionPolicyResponse>.Ok(new GetCommissionPolicyResponse
            {
                MerchantId = policy.MerchantId,
                RatePercent = policy.Margin.RatePercent,
                FixedFee = policy.Margin.FixedFee,
                Status = policy.Status.ToString()
            });
        }
    }
}

public static class GetCommissionPolicyEndpoint
{
    public static RouteGroupBuilder GetCommissionPolicyGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // {merchantId} — MerchantScoped policy claim eşleşmesini buradan okur.
        group.MapGet("/{merchantId:guid}",
                async (Guid merchantId, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<GetCommissionPolicy.GetCommissionPolicyResponse>>(
                            new GetCommissionPolicy.GetCommissionPolicyQuery(merchantId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetCommissionPolicy")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionRead, AuthorizationPolicies.MerchantScoped)
            .Produces<GetCommissionPolicy.GetCommissionPolicyResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
