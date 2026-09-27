namespace Merchant.Api.Utils;

// 048 D4: hassas-veri sayfasına basılan değerler için DAR HTML kaçış (& < > " ').
// Framework HtmlEncoder.Default non-ASCII'yi (Türkçe harfler) VE '+' gibi ASCII'yi numeric
// entity'ye çevirip merchant değerlerini (ad, IBAN, e-posta) BOZARDI. Bu dar set gerçek XSS
// metakarakterlerini kaçırır, kalan her baytı OLDUĞU GİBİ bırakır — HTML gövdesi + TIRNAKLI
// attribute'a hem güvenli hem çıktı-koruyucu (charset=utf-8). ECom 078 refactor deseni.
public static class SensitiveHtmlEncoder
{
    public static string Encode(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");
}
