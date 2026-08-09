
namespace Merchant.Api.ReadModels;

/// <summary>
/// <c>ReferenceDataUpdated</c> tüketicisi (Wolverine assembly taramasıyla keşfedilir). Kind'e göre
/// yerel read-model'i idempotent upsert eder (Marten kimliği = Code → aynı kod overwrite; at-least-once
/// teslimde tekrar zararsız). Tam-set veya diff fark etmez; her kayıt Code anahtarıyla yazılır.
/// NOT: sınıf adı TEKİL "Handler" ile bitmeli — çoğul "Handlers" (eski MerchantEventHandlers)
/// Wolverine 6.4'te sessizce keşfedilmiyor (bkz. CLAUDE.md).
/// </summary>
public static class ReferenceEventHandler
{
    public static async Task Handle(
        IntegrationEvents.ReferenceDataUpdated message,
        IDocumentSession session,
        CancellationToken ct)
    {
        foreach (var item in message.Items)
        {
            switch (message.Kind)
            {
                case "Country":
                    session.Store(new ReferenceCountry { Code = item.Code, Name = item.Name });
                    break;
                case "City":
                    session.Store(new ReferenceCity
                    {
                        Code = item.Code,
                        Name = item.Name,
                        CountryCode = item.CountryCode ?? string.Empty
                    });
                    break;
                case "Mcc":
                    session.Store(new ReferenceMcc { Code = item.Code, Name = item.Name });
                    break;
                case "Bank":
                    session.Store(new ReferenceBank { Code = item.Code, Name = item.Name });
                    break;
            }
        }

        await session.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Reference.Api katalog verisinin yerel izdüşümü (BC izolasyonu: Merchant Reference DB'sine erişmez).
/// Davranışsız read-model satırı (StorefrontView deseni), aggregate değil. <c>ReferenceDataUpdated</c>
/// olayından idempotent upsert edilir; Marten kimliği = <c>Code</c> (Program.cs'te ayarlı). Doğrulama
/// sıcak yolu (onboarding) bu yerel kopyayı okur — senkron dış bağımlılık yok.
/// </summary>
public class ReferenceCountry
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ReferenceCity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

public class ReferenceMcc
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ReferenceBank
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>Read-model kimlik (Code) normalizasyonu. Ülke kodu upper saklanır (seed/event ile tutarlı).</summary>
public static class ReferenceKey
{
    public static string Country(string? code) => code?.Trim().ToUpperInvariant() ?? string.Empty;
}