using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static Shared.IntegrationEvents;

// Sınıf adı TEKİL "Handler" ile bitmeli — çoğul "Handlers" Wolverine 6.4'te keşfedilmiyor (bkz. CLAUDE.md).
namespace Identity.Server.EventHandlers;

// 012: merchant.lifecycle tüketicisi — Merchant BC olaylarını OpenIddict istemci kaydına izdüşürür.
// İdempotent: aynı olay N kez işlense sonuç aynı descriptor. Message store yok (D1) → at-least-once
// teslimde bu idempotency tek güvence, bozma.
public static class MerchantClientEventHandler
{
    // Merchant istemcisi işareti + merchant_id claim kaynağı (TokenEndpoint okur — D6).
    public const string MerchantIdProperty = "merchant_id";

    public static async Task Handle(MerchantCreated e, IOpenIddictApplicationManager apps, ILogger logger)
    {
        var clientId = e.MerchantId.ToString();
        var descriptor = BuildDescriptor(clientId, e.MerchantKey, e.Status);

        var existing = await apps.FindByClientIdAsync(clientId);
        if (existing is null)
        {
            await apps.CreateAsync(descriptor);
            logger.LogInformation("Merchant istemcisi oluşturuldu: {ClientId} (status: {Status})", clientId, e.Status);
        }
        else
        {
            // Yeniden teslim: aynı hedefe update (secret düz metin geldiğinden yeniden hash'lenir, sonuç aynı).
            await apps.UpdateAsync(existing, descriptor);
            logger.LogInformation("Merchant istemcisi güncellendi (yeniden teslim): {ClientId}", clientId);
        }
    }

    public static async Task Handle(MerchantStatusChanged e, IOpenIddictApplicationManager apps, ILogger logger)
    {
        var clientId = e.MerchantId.ToString();
        var existing = await apps.FindByClientIdAsync(clientId);
        if (existing is null)
        {
            // Created henüz işlenmemiş olabilir (sıralama garantisi varsayılmaz) — dev fazında kabul, NO-OP.
            logger.LogWarning("MerchantStatusChanged: istemci bulunamadı, atlanıyor: {ClientId}", clientId);
            return;
        }

        // D4: kayıt ve secret hash'i durur; yalnız izinler açılır/kapanır. PopulateAsync store'daki
        // (hash'li) secret'ı taşır; UpdateAsync değişmeyen secret'ı yeniden hash'lemez.
        var descriptor = new OpenIddictApplicationDescriptor();
        await apps.PopulateAsync(descriptor, existing);

        descriptor.Permissions.Clear();
        if (IsActive(e.NewStatus))
            AddMerchantPermissions(descriptor);

        await apps.UpdateAsync(existing, descriptor);
        logger.LogInformation("Merchant istemci izinleri güncellendi: {ClientId} → {Status}", clientId, e.NewStatus);
    }

    private static OpenIddictApplicationDescriptor BuildDescriptor(string clientId, string merchantKey, string status)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = merchantKey,
            DisplayName = $"Merchant {clientId}",
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
        };

        descriptor.Properties[MerchantIdProperty] = JsonSerializer.SerializeToElement(clientId);

        if (IsActive(status))
            AddMerchantPermissions(descriptor);

        return descriptor;
    }

    // Status-gate (FR-003): yalnız Active izin taşır; izinsiz istemcinin token isteğini
    // OpenIddict reddeder (unauthorized_client).
    private static void AddMerchantPermissions(OpenIddictApplicationDescriptor descriptor)
    {
        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + "merchant.read");
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + "merchant.write");
    }

    private static bool IsActive(string status) =>
        string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
}