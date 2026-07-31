namespace Merchant.Api.Domains.Merchants.Lookups;

/// <summary>MCC referansı (örn. "5411" -> "Grocery Stores").</summary>
public record MccRef(string Code, string Name);

/// <summary>Ülke referansı (örn. "TR" -> "Türkiye").</summary>
public record CountryRef(string Code, string Name);

/// <summary>Şehir referansı; bir ülkeye bağlıdır (örn. "34" -> "İstanbul", "TR").</summary>
public record CityRef(string Code, string Name, string CountryCode);

/// <summary>
/// Kod-içi gömülü standart referans veri. DB'de DEĞİL; açılışta bellekte tutulur.
/// Yönetim ihtiyacı çıkarsa ayrı Reference BC'ye terfi (Obsidian todo). Set v1; genişletilebilir.
/// </summary>
public static class LookupData
{
    public static readonly IReadOnlyList<CountryRef> Countries = new List<CountryRef>
    {
        new("TR", "Türkiye")
    };

    public static readonly IReadOnlyList<CityRef> Cities = new List<CityRef>
    {
        new("34", "İstanbul", "TR"),
        new("06", "Ankara", "TR"),
        new("35", "İzmir", "TR"),
        new("16", "Bursa", "TR"),
        new("07", "Antalya", "TR")
    };

    public static readonly IReadOnlyList<MccRef> Mccs = new List<MccRef>
    {
        new("5411", "Grocery Stores"),
        new("5311", "Department Stores"),
        new("5651", "Family Clothing Stores"),
        new("5732", "Electronics Stores"),
        new("5912", "Drug Stores & Pharmacies"),
        new("5814", "Fast Food Restaurants")
    };
}