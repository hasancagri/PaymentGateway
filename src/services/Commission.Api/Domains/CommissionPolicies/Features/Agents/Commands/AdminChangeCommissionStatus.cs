namespace Commission.Api.Domains.CommissionPolicies.Features.Agents.Commands;

// 044 US3: sohbetten politika statü değişimi — string parse + CommissionPolicy.ChangeStatus
// (geçiş kuralları aggregate'te; aynı statüye geçiş idempotent no-op). Agent slice kendi
// handler'ını taşır (bilinçli tekrar). commission.write kapısı Wolverine middleware'de.
public static class AdminChangeCommissionStatus
{
    [RequiredScope(AuthorizationScopes.CommissionWrite)]
    public record AdminChangeCommissionStatusCommand(Guid MerchantId, string Status);

    public class AdminChangeCommissionStatusResponse
    {
        public Guid PolicyId { get; set; }
        public Guid MerchantId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Transactional]
    public class AdminChangeCommissionStatusCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminChangeCommissionStatusResponse>> Handle(
            AdminChangeCommissionStatusCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (!Enum.TryParse<CommissionPolicyStatus>(cmd.Status?.Trim(), ignoreCase: true, out var target))
                return FeatureObjectResultModel<AdminChangeCommissionStatusResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Status),
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            var policy = await session.Query<CommissionPolicy>()
                .Where(p => p.MerchantId == cmd.MerchantId && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (policy is null)
                return FeatureObjectResultModel<AdminChangeCommissionStatusResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.MerchantId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var changed = policy.ChangeStatus(target);
            if (!changed.IsSuccess)
                return FeatureObjectResultModel<AdminChangeCommissionStatusResponse>.Error(changed.Messages);
            if (changed.Data!)
                session.Store(policy);

            return FeatureObjectResultModel<AdminChangeCommissionStatusResponse>.Ok(
                new AdminChangeCommissionStatusResponse
                {
                    PolicyId = policy.Id,
                    MerchantId = policy.MerchantId,
                    Status = policy.Status.ToString()
                });
        }
    }
}

/// <summary>US3 — politikayı Active/Passive yapar (aynı statüye geçiş idempotent no-op).</summary>
[McpServerToolType]
public static class AdminChangeCommissionStatusMcpTool
{
    [McpServerTool(Name = Shared.CommissionAdminTools.ChangeStatus)]
    [Description("Merchant'ın komisyon politikasının statüsünü değiştirir (Active | Passive). Pasif " +
                 "politika hesaplamada yok sayılır. Aynı statüye geçiş hata DÖNMEZ (idempotent no-op). " +
                 "Politika yoksa kayıt-bulunamadı hatası döner.")]
    public static Task<FeatureObjectResultModel<AdminChangeCommissionStatus.AdminChangeCommissionStatusResponse>>
        AdminChangeCommissionStatusAsync(
            [Description("Politikası değiştirilecek merchant kimliği")] Guid merchantId,
            [Description("Hedef statü: Active | Passive")] string status,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminChangeCommissionStatus.AdminChangeCommissionStatusResponse>>(
            new AdminChangeCommissionStatus.AdminChangeCommissionStatusCommand(merchantId, status), ct);
}