using static Shared.IntegrationEvents;

// Sınıf adı TEKİL "Handler" ile bitmeli — çoğul "Handlers" Wolverine 6.4'te keşfedilmiyor (bkz. CLAUDE.md).
namespace Payment.Api;

/// <summary>
/// Merchant.Api'nin merchant.lifecycle fanout'unun tüketicisi (kaynak = Merchant.Api) — merchant
/// statüsünü Payment BC'nin yerel referansına izdüşürür (çekim statü kapısının veri temeli).
/// İdempotent upsert: aynı olay N kez işlense sonuç aynı. Message store yok → ProcessInline +
/// RabbitMQ redelivery (Payment.Identity MerchantClientEventHandler şablonu).
/// </summary>
public static class MerchantApiConsumers
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

    // 046: key yenileme — KeyHash REPLACE. Doc merchantId (=Id) anahtarlı → Store eski hash'i
    // OVERWRITE eder (eski hash KALMAZ, FR-003). Statü referansına dokunmaz (yenileme statü değiştirmez).
    public static async Task Handle(MerchantKeyReissued e, IDocumentSession session, ILogger logger)
    {
        StoreApiKey(e.MerchantId, e.MerchantKey, session);
        await session.SaveChangesAsync();
        logger.LogInformation("Merchant API-key hash'i yenilendi (reissue): {MerchantId}", e.MerchantId);
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
