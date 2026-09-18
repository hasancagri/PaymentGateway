using Common.Utils.Authorization;

namespace Commission.Api.Domains.CommissionPolicies.Features.Agents.Commands;

// 044 US3: sohbetten komisyon politikası oluşturma. Tekil-aktif kuralı handler sorgusuyla (024
// deseni aynen — aggregate cross-aggregate görmez); kademe doğrulaması MarginTariff.Create'te.
// Agent slice kendi handler'ını taşır (REST CreateCommissionPolicy ile KOD PAYLAŞMAZ, bilinçli
// tekrar). commission.write kapısı Wolverine ScopeAuthorizationMiddleware'de (research R4).
public static class AdminCreateCommissionPolicy
{
    /// <summary>Kademe taşıyıcısı (slice-yerel MCP sözleşmesi); doğrulama MarginTariff.Create'te.</summary>
    public record TierDto(decimal FromAmount, decimal RatePercent, decimal FixedFee);

    [RequiredScope(AuthorizationScopes.CommissionWrite)]
    public record AdminCreateCommissionPolicyCommand(Guid MerchantId, List<TierDto> Tiers);

    public class AdminCreateCommissionPolicyResponse
    {
        public Guid PolicyId { get; set; }
        public Guid MerchantId { get; set; }
        public List<TierDto> Tiers { get; set; } = new();
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class AdminCreateCommissionPolicyCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminCreateCommissionPolicyResponse>> Handle(
            AdminCreateCommissionPolicyCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Merchant başına EN FAZLA bir aktif politika (024 FR-005).
            var existingActive = await session.Query<CommissionPolicy>()
                .Where(p => p.MerchantId == cmd.MerchantId
                    && p.Status == CommissionPolicyStatus.Active
                    && !p.IsDeleted)
                .AnyAsync(ct);
            if (existingActive)
                return FeatureObjectResultModel<AdminCreateCommissionPolicyResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            var tiers = (cmd.Tiers ?? new List<TierDto>())
                .Select(t => (t.FromAmount, t.RatePercent, t.FixedFee)).ToList();
            var result = CommissionPolicy.Create(cmd.MerchantId, tiers);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AdminCreateCommissionPolicyResponse>.Error(result.Messages);

            var policy = result.Data!;
            session.Store(policy);

            return FeatureObjectResultModel<AdminCreateCommissionPolicyResponse>.Ok(
                new AdminCreateCommissionPolicyResponse
                {
                    PolicyId = policy.Id,
                    MerchantId = policy.MerchantId,
                    Tiers = policy.Margin.Tiers
                        .Select(t => new TierDto(t.FromAmount, t.RatePercent, t.FixedFee)).ToList(),
                    Status = policy.Status.ToString()
                });
        }
    }
}

/// <summary>US3 — merchant'a tutar-kademeli marj politikası oluşturur (Active doğar).</summary>
[McpServerToolType]
public static class AdminCreateCommissionPolicyMcpTool
{
    [McpServerTool(Name = Shared.CommissionAdminTools.CreatePolicy)]
    [Description("Merchant'a komisyon (marj) politikası oluşturur. tiers: kademe listesi — fromAmount " +
                 "TL (ilki 0), ratePercent ondalık oran (0.025 = %2,5), fixedFee TL. Merchant'ın zaten " +
                 "aktif politikası varsa duplicate hatası döner (önce statüsünü Passive yapın veya " +
                 "marjı güncelleyin).")]
    public static Task<FeatureObjectResultModel<AdminCreateCommissionPolicy.AdminCreateCommissionPolicyResponse>>
        AdminCreateCommissionPolicyAsync(
            [Description("Politika tanımlanacak merchant kimliği")] Guid merchantId,
            [Description("Kademe listesi: fromAmount (TL, ilki 0), ratePercent (ondalık; 0.025 = %2,5), fixedFee (TL)")]
            List<AdminCreateCommissionPolicy.TierDto> tiers,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminCreateCommissionPolicy.AdminCreateCommissionPolicyResponse>>(
            new AdminCreateCommissionPolicy.AdminCreateCommissionPolicyCommand(merchantId, tiers), ct);
}