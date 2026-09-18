namespace Merchant.Api.Domains.Merchants.Features.Agents.Commands;

// 043 US1: AdminActivateMerchant ile birebir aynı desen, hedef Suspended (bilinçli tekrar).
public static class AdminSuspendMerchant
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminSuspendMerchantCommand(Guid MerchantId);

    public class AdminSuspendMerchantResponse
    {
        public Guid MerchantId { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public bool Changed { get; set; }
    }

    [Transactional]
    public class AdminSuspendMerchantCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminSuspendMerchantResponse>> Handle(
            AdminSuspendMerchantCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<AdminSuspendMerchantResponse>.NotFound();

            var previousStatus = merchant.Status;
            var changed = merchant.ChangeStatus(MerchantStatus.Suspended);
            if (changed.Data!)
            {
                session.Store(merchant);

                // 044: REST ChangeMerchantStatus söküldü — statü yayını artık buradan (Identity + Payment tüketir).
                await bus.PublishAsync(new Shared.IntegrationEvents.MerchantStatusChanged(
                    merchant.Id, merchant.Status.ToString()));
            }

            return FeatureObjectResultModel<AdminSuspendMerchantResponse>.Ok(new AdminSuspendMerchantResponse
            {
                MerchantId = merchant.Id,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = merchant.Status.ToString(),
                Changed = changed.Data!
            });
        }
    }
}

/// <summary>US1 — merchant'ı Suspended durumuna alır (idempotent no-op — zaten Suspended ise changed=false).</summary>
[McpServerToolType]
public static class AdminSuspendMerchantMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.SuspendMerchant)]
    [Description("Merchant'ı Suspended durumuna alır. Zaten Suspended ise hata DÖNMEZ, changed=false ile " +
                 "idempotent no-op sonucu döner.")]
    public static Task<FeatureObjectResultModel<AdminSuspendMerchant.AdminSuspendMerchantResponse>>
        AdminSuspendMerchantAsync(
            [Description("Statüsü değiştirilecek merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminSuspendMerchant.AdminSuspendMerchantResponse>>(
            new AdminSuspendMerchant.AdminSuspendMerchantCommand(merchantId), ct);
}
