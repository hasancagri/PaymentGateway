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

    // 013: aktivasyon anında (key teslim) client provision — Provisioning demeti (merchant.read/write;
    // charge HARİÇ). "Aktivasyon öncesi token yok" = bu event gelmeden client YOK (fail-closed).
    // Onboarding'de MerchantCreated'ın yerini alır (aynı idempotent upsert mantığı).
    public static async Task Handle(MerchantProvisioned e, IOpenIddictApplicationManager apps, ILogger logger)
    {
        var clientId = e.MerchantId.ToString();
        var descriptor = BuildDescriptor(clientId, e.MerchantKey, e.Status);

        var existing = await apps.FindByClientIdAsync(clientId);
        if (existing is null)
        {
            await apps.CreateAsync(descriptor);
            logger.LogInformation("Merchant istemcisi provision edildi: {ClientId} (status: {Status})", clientId, e.Status);
        }
        else
        {
            await apps.UpdateAsync(existing, descriptor);
            logger.LogInformation("Merchant istemcisi güncellendi (provision, yeniden teslim): {ClientId}", clientId);
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
        if (GrantsToken(e.NewStatus))
            AddMerchantPermissions(descriptor, e.NewStatus);

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

        if (GrantsToken(status))
            AddMerchantPermissions(descriptor, status);

        return descriptor;
    }

    // 013 kademeli yetki: token yalnız Provisioning + Active statüde verilir (charge hiçbir alt-statüde
    // — henüz yok; demetler bugün eşdeğer, gate kurulur). Passive/Suspended → izinsiz (unauthorized_client).
    private static void AddMerchantPermissions(OpenIddictApplicationDescriptor descriptor, string status)
    {
        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + "merchant.read");
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + "merchant.write");

        // 017: cards.write yalnız Active demetine (vault ödeme düzlemi). Provisioning ALMAZ →
        // charge fail-closed korunur (FR-017). payment.write hiçbir statüde merchant'a verilmez.
        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + "cards.write");
    }

    private static bool GrantsToken(string status) =>
        string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Provisioning", StringComparison.OrdinalIgnoreCase);
}