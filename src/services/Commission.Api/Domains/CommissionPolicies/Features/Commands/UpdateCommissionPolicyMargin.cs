using Commission.Api.Domains.CommissionPolicies;

namespace Commission.Api.Domains.CommissionPolicies.Features.Commands;

// 024: mevcut politikanın marjını güncelleme (US1/FR-002). merchantId'den aktif politika bulunur;
// yoksa RECORD_NOT_FOUND. İleriye dönük (geçmiş hesaplar etkilenmez). AdminPlaneOnly.
public static class UpdateCommissionPolicyMargin
{
    public record UpdateCommissionPolicyMarginCommand(Guid MerchantId, List<TierDto> Tiers);

    public class UpdateCommissionPolicyMarginResponse
    {
        public Guid PolicyId { get; set; }
        public Guid MerchantId { get; set; }
        public List<TierDto> Tiers { get; set; } = new();
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class UpdateCommissionPolicyMarginCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateCommissionPolicyMarginResponse>> Handle(
            UpdateCommissionPolicyMarginCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var policy = await session.Query<CommissionPolicy>()
                .Where(p => p.MerchantId == cmd.MerchantId && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (policy is null)
                return FeatureObjectResultModel<UpdateCommissionPolicyMarginResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var tiers = (cmd.Tiers ?? new List<TierDto>())
                .Select(t => (t.FromAmount, t.RatePercent, t.FixedFee)).ToList();
            var updated = policy.UpdateMargin(tiers);
            if (!updated.IsSuccess)
                return FeatureObjectResultModel<UpdateCommissionPolicyMarginResponse>.Error(updated.Messages);

            session.Store(policy);

            return FeatureObjectResultModel<UpdateCommissionPolicyMarginResponse>.Ok(new UpdateCommissionPolicyMarginResponse
            {
                PolicyId = policy.Id,
                MerchantId = policy.MerchantId,
                Tiers = policy.Margin.Tiers.Select(t => new TierDto(t.FromAmount, t.RatePercent, t.FixedFee)).ToList(),
                Status = policy.Status.ToString()
            });
        }
    }
}

public static class UpdateCommissionPolicyMarginEndpoint
{
    public record UpdateCommissionPolicyMarginBody(List<TierDto> Tiers);

    public static RouteGroupBuilder UpdateCommissionPolicyMarginGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{merchantId:guid}/margin",
                async (Guid merchantId, [FromBody] UpdateCommissionPolicyMarginBody body, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<UpdateCommissionPolicyMargin.UpdateCommissionPolicyMarginResponse>>(
                            new UpdateCommissionPolicyMargin.UpdateCommissionPolicyMarginCommand(
                                merchantId, body.Tiers));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("UpdateCommissionPolicyMargin")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<UpdateCommissionPolicyMargin.UpdateCommissionPolicyMarginResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
