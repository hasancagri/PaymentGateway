namespace Merchant.Api.Domains.Merchants.Features.Agents.Commands;

// 043 US1: admin yüzeyi — sabit hedef statüyle Merchant.ChangeStatus çağırır (agent slice kendi
// command+handler'ını taşır, mevcut REST ChangeMerchantStatus slice'ıyla KOD PAYLAŞMAZ — bilinçli
// tekrar, conventions.md). merchant.admin scope'u ScopeAuthorizationMiddleware ile fail-closed.
public static class AdminActivateMerchant
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    public record AdminActivateMerchantCommand(Guid MerchantId);

    public class AdminActivateMerchantResponse
    {
        public Guid MerchantId { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public bool Changed { get; set; }
    }

    [Transactional]
    public class AdminActivateMerchantCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminActivateMerchantResponse>> Handle(
            AdminActivateMerchantCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null || merchant.IsDeleted)
                return FeatureObjectResultModel<AdminActivateMerchantResponse>.NotFound();

            var previousStatus = merchant.Status;
            var changed = merchant.ChangeStatus(MerchantStatus.Active);
            if (changed.Data!)
            {
                session.Store(merchant);

                // 044: REST ChangeMerchantStatus söküldü — statü yayını artık buradan. Identity.Server
                // tüketir (token kapısı) + Payment MerchantStatus referansı beslenir; outbox commit'le atomik.
                await bus.PublishAsync(new Shared.IntegrationEvents.MerchantStatusChanged(
                    merchant.Id, merchant.Status.ToString()));
            }

            return FeatureObjectResultModel<AdminActivateMerchantResponse>.Ok(new AdminActivateMerchantResponse
            {
                MerchantId = merchant.Id,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = merchant.Status.ToString(),
                Changed = changed.Data!
            });
        }
    }
}

// 043: MCP tool ince sarmalayıcıdır ve YALNIZ yukarıdaki slice'ı IMessageBus ile çağırır. Tool adı
// DIŞ SÖZLEŞMEDİR (Shared.MerchantAdminTools.ActivateMerchant); değiştirme. Yüzey: /mcp, merchant.write
// (endpoint-seviye) + merchant.admin (tool-seviye, Wolverine middleware).
/// <summary>US1 — merchant'ı Active durumuna alır (idempotent no-op — zaten Active ise changed=false).</summary>
[McpServerToolType]
public static class AdminActivateMerchantMcpTool
{
    [McpServerTool(Name = Shared.MerchantAdminTools.ActivateMerchant)]
    [Description("Merchant'ı Active durumuna alır. Zaten Active ise hata DÖNMEZ, changed=false ile " +
                 "idempotent no-op sonucu döner.")]
    public static Task<FeatureObjectResultModel<AdminActivateMerchant.AdminActivateMerchantResponse>>
        AdminActivateMerchantAsync(
            [Description("Statüsü değiştirilecek merchant kimliği")] Guid merchantId,
            IMessageBus bus,
            CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminActivateMerchant.AdminActivateMerchantResponse>>(
            new AdminActivateMerchant.AdminActivateMerchantCommand(merchantId), ct);
}
