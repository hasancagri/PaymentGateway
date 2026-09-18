namespace Merchant.Api.Domains.Merchants.Features.Agents.Commands;

// 043 US1: AdminActivateMerchant ile birebir aynı desen, hedef Passive (bilinçli tekrar).
public static class AdminDeactivateMerchant
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminDeactivateMerchantCommand(Guid MerchantId);

    public class AdminDeactivateMerchantResponse
    {
        public Guid MerchantId { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public bool Changed { get; set; }
    }

    [Transactional]
    public class AdminDeactivateMerchantCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminDeactivateMerchantResponse>> Handle(
            AdminDeactivateMerchantCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<AdminDeactivateMerchantResponse>.NotFound();

            var previousStatus = merchant.Status;
            var changed = merchant.ChangeStatus(MerchantStatus.Passive);
            if (changed.Data!)
            {
                session.Store(merchant);

                // 044: REST ChangeMerchantStatus söküldü — statü yayını artık buradan (Identity + Payment tüketir).
                await bus.PublishAsync(new Shared.IntegrationEvents.MerchantStatusChanged(
                    merchant.Id, merchant.Status.ToString()));
            }

            return FeatureObjectResultModel<AdminDeactivateMerchantResponse>.Ok(new AdminDeactivateMerchantResponse
            {
                MerchantId = merchant.Id,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = merchant.Status.ToString(),
                Changed = changed.Data!
            });
        }
    }
}

/// <summary>US1 — merchant'ı Passive durumuna alır (idempotent no-op — zaten Passive ise changed=false).</summary>
[McpServerToolType]
public static class AdminDeactivateMerchantMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.DeactivateMerchant)]
    [Description("Merchant'ı Passive durumuna alır. Zaten Passive ise hata DÖNMEZ, changed=false ile " +
                 "idempotent no-op sonucu döner.")]
    public static Task<FeatureObjectResultModel<AdminDeactivateMerchant.AdminDeactivateMerchantResponse>>
        AdminDeactivateMerchantAsync(
            [Description("Statüsü değiştirilecek merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminDeactivateMerchant.AdminDeactivateMerchantResponse>>(
            new AdminDeactivateMerchant.AdminDeactivateMerchantCommand(merchantId), ct);
}
