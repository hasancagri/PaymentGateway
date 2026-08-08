using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;
using static Shared.IntegrationEvents;

// Sınıf adı TEKİL "Handler" ile bitmeli — çoğul "Handlers" Wolverine 6.4'te sessizce keşfedilmiyor
// (bkz. CLAUDE.md). Canlı doğrulama: "Successfully processed message", "No known handler" YOK.
namespace Merchant.Api.ReadModels;

/// <summary>
/// 013 US4/US5 — Commission.Api'nin <c>MerchantCommissionGridReady</c> event'ini tüketir (Active
/// koşulu #2). <c>MarkCommissionGridReady</c> + <c>TryActivate</c> (idempotent, durable inbox).
/// Active'e geçerse <c>MerchantStatusChanged(Active)</c> yayınlanır (aynı [Transactional] = outbox).
/// At-least-once: zaten Ready/Active ise no-op.
/// </summary>
public static class MerchantCommissionGridReadyHandler
{
    [Transactional]
    public static async Task Handle(
        MerchantCommissionGridReady message,
        IDocumentSession session,
        IMessageBus bus,
        ILogger<MerchantCommissionGridReadyHandlerLog> logger,
        CancellationToken ct)
    {
        var merchant = await session.LoadAsync<MerchantAggregate>(message.MerchantId, ct);
        if (merchant is null)
        {
            logger.LogWarning("MerchantCommissionGridReady: merchant bulunamadı, atlanıyor: {MerchantId}",
                message.MerchantId);
            return;
        }

        merchant.MarkCommissionGridReady();
        var activated = merchant.TryActivate().IsSuccess;
        session.Update(merchant);

        if (activated)
            await bus.PublishAsync(new MerchantStatusChanged(merchant.Id, "Active"));
    }
}

/// <summary>ILogger kategori işareti (statik handler için tip adı).</summary>
public sealed class MerchantCommissionGridReadyHandlerLog;
