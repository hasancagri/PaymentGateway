using Commission.Api.Domains.CommissionPolicies;

namespace Commission.Api.Domains.CommissionPolicies.Features.Queries;

// 024: efektif komisyon + net hakediş hesabı (US2/FR-006..009). Durum değiştirmez (Query). iyzico
// maliyeti string girdi (işlem-sonrası rapordan — FR-012). Aktif politika handler-lookup; yoksa
// RECORD_NOT_FOUND (sessiz 0 YOK — SC-003). Aritmetik/tutarsızlık aggregate'te. AdminPlaneOnly
// (sistem/admin çağırır; makine token'ı claim'siz geçer). POST — gövde taşımak için (GET gövdesiz).
public static class CalculateEffectiveCommission
{
    public record CalculateEffectiveCommissionQuery(
        Guid MerchantId,
        decimal PaidPrice,
        string IyzicoCommission,
        string IyzicoFee,
        int Installment);

    public class CalculateEffectiveCommissionResponse
    {
        public Guid MerchantId { get; set; }
        public decimal PaidPrice { get; set; }
        public int Installment { get; set; }
        public decimal IyzicoCost { get; set; }
        public decimal GatewayMargin { get; set; }
        public decimal TotalEffectiveCommission { get; set; }
        public decimal NetPayout { get; set; }
    }

    public class CalculateEffectiveCommissionQueryHandler
    {
        public async Task<FeatureObjectResultModel<CalculateEffectiveCommissionResponse>> Handle(
            CalculateEffectiveCommissionQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var policy = await session.Query<CommissionPolicy>()
                .Where(p => p.MerchantId == query.MerchantId
                    && p.Status == CommissionPolicyStatus.Active
                    && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (policy is null)
                return FeatureObjectResultModel<CalculateEffectiveCommissionResponse>.Error(new MessageItem
                {
                    Property = nameof(query.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = policy.CalculateEffectiveCommission(
                query.PaidPrice, query.IyzicoCommission, query.IyzicoFee, query.Installment);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CalculateEffectiveCommissionResponse>.Error(result.Messages);

            var ec = result.Data!;
            return FeatureObjectResultModel<CalculateEffectiveCommissionResponse>.Ok(new CalculateEffectiveCommissionResponse
            {
                MerchantId = policy.MerchantId,
                PaidPrice = ec.PaidPrice,
                Installment = ec.Installment,
                IyzicoCost = ec.IyzicoCost,
                GatewayMargin = ec.GatewayMargin,
                TotalEffectiveCommission = ec.TotalEffectiveCommission,
                NetPayout = ec.NetPayout
            });
        }
    }
}

public static class CalculateEffectiveCommissionEndpoint
{
    public static RouteGroupBuilder CalculateEffectiveCommissionGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/effective-commission",
                async ([FromBody] CalculateEffectiveCommission.CalculateEffectiveCommissionQuery query, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<CalculateEffectiveCommission.CalculateEffectiveCommissionResponse>>(query);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CalculateEffectiveCommission")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionRead, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<CalculateEffectiveCommission.CalculateEffectiveCommissionResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
