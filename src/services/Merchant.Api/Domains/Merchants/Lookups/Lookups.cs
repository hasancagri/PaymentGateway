using Common.Dependencies;

namespace Merchant.Api.Domains.Merchants.Lookups;

public interface IMccLookup : ISingletonDependency
{
    bool Exists(string code);
    string? NameOf(string code);
}

public interface ICountryLookup : ISingletonDependency
{
    bool Exists(string code);
    string? NameOf(string code);
}

public interface ICityLookup : ISingletonDependency
{
    bool Exists(string code);
    string? NameOf(string code);

    /// <summary>Şehir kodu verilen ülkeye ait mi? (çapraz lookup tutarlılığı)</summary>
    bool BelongsTo(string cityCode, string countryCode);
}

/// <summary>Gömülü MCC verisini bellekte tutar (singleton).</summary>
public class MccLookup : IMccLookup
{
    private readonly Dictionary<string, MccRef> _byCode =
        LookupData.Mccs.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);

    public bool Exists(string code) => code is not null && _byCode.ContainsKey(code);

    public string? NameOf(string code) =>
        code is not null && _byCode.TryGetValue(code, out var m) ? m.Name : null;
}

/// <summary>Gömülü ülke verisini bellekte tutar (singleton).</summary>
public class CountryLookup : ICountryLookup
{
    private readonly Dictionary<string, CountryRef> _byCode =
        LookupData.Countries.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    public bool Exists(string code) => code is not null && _byCode.ContainsKey(code);

    public string? NameOf(string code) =>
        code is not null && _byCode.TryGetValue(code, out var c) ? c.Name : null;
}

/// <summary>Gömülü şehir verisini bellekte tutar (singleton).</summary>
public class CityLookup : ICityLookup
{
    private readonly Dictionary<string, CityRef> _byCode =
        LookupData.Cities.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    public bool Exists(string code) => code is not null && _byCode.ContainsKey(code);

    public string? NameOf(string code) =>
        code is not null && _byCode.TryGetValue(code, out var c) ? c.Name : null;

    public bool BelongsTo(string cityCode, string countryCode) =>
        cityCode is not null && countryCode is not null &&
        _byCode.TryGetValue(cityCode, out var c) &&
        string.Equals(c.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase);
}