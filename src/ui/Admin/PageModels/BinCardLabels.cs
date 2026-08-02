namespace Admin.PageModels;

/// <summary>
/// Payment.Api'den gelen enum **adlarını** Türkçe okunur etikete çevirir (sunum). Backend'e kural
/// sızdırmaz — yalnız görüntü. Tanınmayan ad → adın kendisi (çökme yok). Açılır liste seçenekleri de
/// buradan (Payment.Api enum adlarıyla birebir).
/// </summary>
public static class BinCardLabels
{
    private static readonly Dictionary<string, string> TypeMap = new()
    {
        ["Credit"] = "Kredi",
        ["Debit"] = "Banka",
    };

    private static readonly Dictionary<string, string> BrandMap = new()
    {
        ["Unknown"] = "Bilinmiyor",
        ["Visa"] = "Visa",
        ["MasterCard"] = "MasterCard",
        ["Troy"] = "Troy",
        ["Amex"] = "Amex",
        ["Discover"] = "Discover",
        ["Unionpay"] = "UnionPay",
        ["JCB"] = "JCB",
    };

    private static readonly Dictionary<string, string> ProgramMap = new()
    {
        ["Unknown"] = "Bilinmiyor",
        ["Axess"] = "Axess",
        ["Bank24"] = "Bank24",
        ["Bankkart"] = "Bankkart",
        ["Bonus"] = "Bonus",
        ["CardFinans"] = "CardFinans",
        ["Maximum"] = "Maximum",
        ["MilesAndSmiles"] = "Miles&Smiles",
        ["Neo"] = "Neo",
        ["Paraf"] = "Paraf",
        ["ShopAndFly"] = "Shop&Fly",
        ["Wings"] = "Wings",
        ["World"] = "World",
        ["Advantage"] = "Advantage",
        ["SaglamKart"] = "SağlamKart",
    };

    public static string Type(string name) => Lookup(TypeMap, name);
    public static string Brand(string name) => Lookup(BrandMap, name);
    public static string Program(string name) => Lookup(ProgramMap, name);
    public static string Commercial(bool value) => value ? "Evet" : "Hayır";

    /// <summary>Açılır liste seçenekleri: (deger = Payment.Api enum adı, etiket = Türkçe).</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> TypeOptions => TypeMap.ToList();
    public static IReadOnlyList<KeyValuePair<string, string>> BrandOptions => BrandMap.ToList();
    public static IReadOnlyList<KeyValuePair<string, string>> ProgramOptions => ProgramMap.ToList();

    private static string Lookup(Dictionary<string, string> map, string name) =>
        !string.IsNullOrEmpty(name) && map.TryGetValue(name, out var label) ? label : name;
}