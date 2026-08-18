using static Shared.IntegrationEvents;

// Sınıf adı TEKİL "Handler" ile bitmeli — çoğul "Handlers" Wolverine 6.4'te keşfedilmiyor (bkz. CLAUDE.md).
namespace Payment.Api.Domains.MerchantStatus;

/// <summary>
/// 038: merchant.lifecycle tüketicisi — merchant statüsünü Payment BC'nin yerel referansına izdüşürür
/// (çekim statü kapısının veri temeli). İdempotent upsert: aynı olay N kez işlense sonuç aynı.
/// Message store yok → ProcessInline + RabbitMQ redelivery (Identity.Server MerchantClientEventHandler şablonu).
/// </summary>
public static class MerchantLifecycleEventHandler
{
    public static async Task Handle(MerchantCreated e, IDocumentSession session, ILogger logger)
    {
        StoreApiKey(e.MerchantId, e.MerchantKey, session); // 039: key yalnız Created/Provisioned'da gelir
        await Upsert(e.MerchantId, e.Status, session, logger);
    }

    public static async Task Handle(MerchantProvisioned e, IDocumentSession session, ILogger logger)
    {
        StoreApiKey(e.MerchantId, e.MerchantKey, session);
        await Upsert(e.MerchantId, e.Status, session, logger);
    }

    public static async Task Handle(MerchantStatusChanged e, IDocumentSession session, ILogger logger)
    {
        // StatusChanged key TAŞIMAZ → API-key referansına dokunma (var olan hash korunur).
        await Upsert(e.MerchantId, e.NewStatus, session, logger);
    }

    // 039: X-Api-Key auth için merchant key'in SHA-256 hash'ini kiracı referansına yazar (idempotent).
    private static void StoreApiKey(Guid merchantId, string merchantKey, IDocumentSession session)
    {
        if (string.IsNullOrWhiteSpace(merchantKey)) return;
        session.Store(new MerchantApiKeyReference
        {
            Id = merchantId,
            KeyHash = ApiKeyHash.Compute(merchantKey)
        });
    }

    private static async Task Upsert(Guid merchantId, string status, IDocumentSession session, ILogger logger)
    {
        session.Store(new MerchantStatusReference
        {
            Id = merchantId,
            Status = status,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await session.SaveChangesAsync();
        logger.LogInformation("Merchant statü referansı güncellendi: {MerchantId} → {Status}", merchantId, status);
    }
}