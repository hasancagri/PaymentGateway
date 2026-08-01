using Common.Dependencies;

namespace Merchant.Api.Domains.MerchantSettlementAccounts.Lookups;

/// <summary>
/// Banka kodu doğrulama + ad türetimi. <c>BankCatalog</c>'a karşı bellek-içi lookup (mevcut
/// MCC/Country/City lookup deseni). Scrutor <c>ISingletonDependency</c> ile otomatik kaydeder.
/// </summary>
public interface IBankCodeLookup : ISingletonDependency
{
    bool Exists(string code);
    string? NameOf(string code);
}

/// <summary>Gömülü banka katalogunu bellekte tutar (singleton).</summary>
public class BankCodeLookup : IBankCodeLookup
{
    private readonly Dictionary<string, string> _byCode =
        BankCatalog.All.ToDictionary(e => e.Code, e => e.Name, StringComparer.OrdinalIgnoreCase);

    public bool Exists(string code) => code is not null && _byCode.ContainsKey(code);

    public string? NameOf(string code) =>
        code is not null && _byCode.TryGetValue(code, out var name) ? name : null;
}