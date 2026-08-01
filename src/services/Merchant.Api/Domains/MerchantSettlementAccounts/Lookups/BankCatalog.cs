namespace Merchant.Api.Domains.MerchantSettlementAccounts.Lookups;

/// <summary>
/// Seçilebilir bankaların kanonik katalogu (banka kodu + adı). <c>Commission.Api</c>'deki
/// <c>BankCatalog</c> ile ELLE SENKRON tutulan yerel kopyadır — BC izolasyonu gereği Merchant BC
/// Commission BC'nin verisine runtime erişemez (bkz. research D1). Değişiklik nadir; yeni banka
/// gerekince her iki katalog da elle güncellenir.
/// </summary>
public static class BankCatalog
{
    public record CatalogEntry(string Code, string Name);

    public static IReadOnlyList<CatalogEntry> All { get; } = new List<CatalogEntry>
    {
        new("0046", "Akbank"),
        new("9046", "Akbank Nestpay"),
        new("0203", "Albaraka Türk"),
        new("0124", "Alternatif Bank"),
        new("0135", "Anadolubank"),
        new("0134", "Denizbank"),
        new("0103", "Fibabanka"),
        new("0111", "QNB Finansbank"),
        new("9111", "Finansbank Nestpay"),
        new("0062", "Garanti BBVA"),
        new("0012", "Halkbank"),
        new("0123", "HSBC"),
        new("0099", "ING Bank"),
        new("0064", "İş Bankası"),
        new("0205", "Kuveyt Türk"),
        new("0146", "Odeabank"),
        new("0032", "Türk Ekonomi Bankası"),
        new("0206", "Türkiye Finans"),
        new("0015", "Vakıfbank"),
        new("0067", "Yapı Kredi Bankası"),
        new("0059", "Şekerbank"),
        new("0010", "Ziraat Bankası"),
        new("0143", "Aktif Yatırım Bankası"),
        new("0210", "Vakıf Katılım"),
        new("0209", "Ziraat Katılım"),
        new("9977", "Paynet"),
        new("9978", "PayNKolay"),
        new("9979", "HalkÖde"),
        new("9980", "Tami"),
        new("9981", "VakıfPayS"),
        new("9982", "ZiraatPay"),
        new("9983", "Vepara"),
        new("9984", "Moka"),
        new("9985", "Ahlpay"),
        new("9986", "IQmoney"),
        new("9987", "Parolapara"),
        new("9988", "PayBull"),
        new("9989", "ParamPos"),
        new("9990", "QNBpay"),
        new("9991", "Sipay"),
        new("9992", "Hepsipay"),
        new("9993", "Payten"),
        new("9994", "PayTR"),
        new("9995", "IPara"),
        new("9996", "PayU"),
        new("9997", "Iyzico"),
        new("9998", "Cardplus"),
        new("9999", "Paratika"),
    };

    private static readonly Dictionary<string, string> ByCode =
        All.ToDictionary(e => e.Code, e => e.Name);

    /// <summary>Kod katalogda varsa adını verir. Doğrulama + Name türetimi.</summary>
    public static bool TryGetName(string code, out string name)
    {
        if (code is not null && ByCode.TryGetValue(code, out var found))
        {
            name = found;
            return true;
        }

        name = string.Empty;
        return false;
    }
}