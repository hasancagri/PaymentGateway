using Commission.Api.Domains.CommissionPolicies;

namespace Commission.Api.Domains.CommissionPolicies.Features.Commands;

// 024: admin düzleminden gateway marj politikası oluşturma (US1). Marj doğrulaması aggregate'te
// (oran/ücret cap). Tekil-aktif kuralı (FR-005) burada handler-sorgusuyla uygulanır — aggregate
// başka aggregate'i göremez. AdminPlaneOnly: claim'li merchant token'ı giremez.
public static class CreateCommissionPolicy
{
    /// <summary>Kademe taşıyıcısı (slice-yerel HTTP sözleşmesi); doğrulama MarginTariff.Create'te.</summary>
    public record TierDto(decimal FromAmount, decimal RatePercent, decimal FixedFee);

    public record CreateCommissionPolicyCommand(Guid MerchantId, List<TierDto> Tiers);

    public class CreateCommissionPolicyResponse
    {
        public Guid PolicyId { get; set; }
        public Guid MerchantId { get; set; }
        public List<TierDto> Tiers { get; set; } = new();
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class CreateCommissionPolicyCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateCommissionPolicyResponse>> Handle(
            CreateCommissionPolicyCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // FR-005: merchant başına EN FAZLA bir aktif politika.
            var existingActive = await session.Query<CommissionPolicy>()
                .Where(p => p.MerchantId == cmd.MerchantId
                    && p.Status == CommissionPolicyStatus.Active
                    && !p.IsDeleted)
                .AnyAsync(ct);
            if (existingActive)
                return FeatureObjectResultModel<CreateCommissionPolicyResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            var tiers = (cmd.Tiers ?? new List<TierDto>())
                .Select(t => (t.FromAmount, t.RatePercent, t.FixedFee)).ToList();
            var result = CommissionPolicy.Create(cmd.MerchantId, tiers);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreateCommissionPolicyResponse>.Error(result.Messages);

            var policy = result.Data!;
            session.Store(policy);

            return FeatureObjectResultModel<CreateCommissionPolicyResponse>.Ok(new CreateCommissionPolicyResponse
            {
                PolicyId = policy.Id,
                MerchantId = policy.MerchantId,
                Tiers = policy.Margin.Tiers.Select(t => new TierDto(t.FromAmount, t.RatePercent, t.FixedFee)).ToList(),
                Status = policy.Status.ToString()
            });
        }
    }
}

public static class CreateCommissionPolicyEndpoint
{
    public static RouteGroupBuilder CreateCommissionPolicyGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreateCommissionPolicy.CreateCommissionPolicyCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<CreateCommissionPolicy.CreateCommissionPolicyResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateCommissionPolicy")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.CommissionWrite, AuthorizationPolicies.AdminPlaneOnly)
            .Produces<CreateCommissionPolicy.CreateCommissionPolicyResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return group;
    }
}
