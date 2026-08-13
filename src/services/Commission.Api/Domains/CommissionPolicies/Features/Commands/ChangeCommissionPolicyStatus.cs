using Commission.Api.Domains.CommissionPolicies;

namespace Commission.Api.Domains.CommissionPolicies.Features.Commands;

// 024: politika statü yönetimi (US1/FR-003). Pasif politika hesaplamada yok sayılır. Aynı statüye
// geçiş idempotent no-op (değişiklik yok). AdminPlaneOnly.
public static class ChangeCommissionPolicyStatus
{
    public record ChangeCommissionPolicyStatusCommand(Guid MerchantId, string Status);

    public class ChangeCommissionPolicyStatusResponse
    {
        public Guid MerchantId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class ChangeCommissionPolicyStatusCommandHandler
    {
        public async Task<FeatureObjectResultModel<ChangeCommissionPolicyStatusResponse>> Handle(
            ChangeCommissionPolicyStatusCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (!Enum.TryParse<CommissionPolicyStatus>(cmd.Status?.Trim(), ignoreCase: true, out var target))
                return FeatureObjectResultModel<ChangeCommissionPolicyStatusResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Status),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            var policy = await session.Query<CommissionPolicy>()
                .Where(p => p.MerchantId == cmd.MerchantId && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (policy is null)
                return FeatureObjectResultModel<ChangeCommissionPolicyStatusResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var changed = policy.ChangeStatus(target);
            if (changed.Data!)
                session.Store(policy);

            return FeatureObjectResultModel<ChangeCommissionPolicyStatusResponse>.Ok(new ChangeCommissionPolicyStatusResponse
            {
                MerchantId = policy.MerchantId,
                Status = policy.Status.ToString()
            });
        }
    }
}

public static class ChangeCommissionPolicyStatusEndpoint
{
    public record ChangeCommissionPolicyStatusBody(string Status);

    public static RouteGroupBuilder ChangeCommissionPolicyStatusGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{merchantId:guid}/status",
                async (Guid merchantId, [FromBody] ChangeCommissionPolicyStatusBody body, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<ChangeCommissionPolicyStatus.ChangeCommissionPolicyStatusResponse>>(
                            new ChangeCommissionPolicyStatus.ChangeCommissionPolicyStatusCommand(merchantId, body.Status));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("ChangeCommissionPolicyStatus")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<ChangeCommissionPolicyStatus.ChangeCommissionPolicyStatusResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
