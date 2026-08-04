namespace Merchant.Api.ReadModels;

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