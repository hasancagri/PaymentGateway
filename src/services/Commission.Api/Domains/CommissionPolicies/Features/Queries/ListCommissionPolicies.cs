using Commission.Api.Domains.CommissionPolicies;

namespace Commission.Api.Domains.CommissionPolicies.Features.Queries;

// 024: admin genel bakış — politika listesi (opsiyonel merchantId/status filtre). AdminPlaneOnly:
// merchant token'ı tüm listeyi göremez. Boş liste geçerli sonuçtur.
public static class ListCommissionPolicies
{
    public record ListCommissionPoliciesQuery(Guid? MerchantId, string? Status);

    public class ListCommissionPoliciesResponse
    {
        public List<CommissionPolicyItem> Policies { get; set; } = new();
    }

    public class CommissionPolicyItem
    {
        public Guid PolicyId { get; set; }
        public Guid MerchantId { get; set; }
        public decimal RatePercent { get; set; }
        public decimal FixedFee { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
    }

    public class ListCommissionPoliciesQueryHandler
    {
        public async Task<FeatureObjectResultModel<ListCommissionPoliciesResponse>> Handle(
            ListCommissionPoliciesQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var q = session.Query<CommissionPolicy>().Where(p => !p.IsDeleted);

            if (query.MerchantId is { } merchantId && merchantId != Guid.Empty)
                q = q.Where(p => p.MerchantId == merchantId);

            if (Enum.TryParse<CommissionPolicyStatus>(query.Status?.Trim(), ignoreCase: true, out var status))
                q = q.Where(p => p.Status == status);

            var policies = await q.OrderByDescending(p => p.CreatedTime).ToListAsync(ct);

            return FeatureObjectResultModel<ListCommissionPoliciesResponse>.Ok(new ListCommissionPoliciesResponse
            {
                Policies = policies.Select(p => new CommissionPolicyItem
                {
                    PolicyId = p.Id,
                    MerchantId = p.MerchantId,
                    RatePercent = p.Margin.RatePercent,
                    FixedFee = p.Margin.FixedFee,
                    Status = p.Status.ToString(),
                    CreatedTime = p.CreatedTime
                }).ToList()
            });
        }
    }
}

public static class ListCommissionPoliciesEndpoint
{
    public static RouteGroupBuilder ListCommissionPoliciesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/",
                async (Guid? merchantId, string? status, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<ListCommissionPolicies.ListCommissionPoliciesResponse>>(
                            new ListCommissionPolicies.ListCommissionPoliciesQuery(merchantId, status));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ListCommissionPolicies")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionRead, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<ListCommissionPolicies.ListCommissionPoliciesResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
