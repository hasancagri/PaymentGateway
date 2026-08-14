using Payment.Api.Domains.StoredCards;

namespace Payment.Api.CardVault;

/// <summary>
/// PAN üzerinden saf yardımcılar (altyapı, aggregate DEĞİL → private helper serbest). Tokenize
/// anında Luhn doğrulama + bin/last4/brand türetimi. Hepsi deterministik, saf → domain birim testi.
/// </summary>
public static class LuhnValidator
{
    /// <summary>ISO/IEC 7812 Luhn kontrol basamağı doğrulaması. Yalnız rakam + 12–19 hane kabul.</summary>
    public static bool IsValid(string? pan)
    {
        if (string.IsNullOrWhiteSpace(pan))
            return false;

        var digits = pan.Trim();
        if (digits.Length < 12 || digits.Length > 19)
            return false;

        var sum = 0;
        var doubleDigit = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var c = digits[i];
            if (c < '0' || c > '9')
                return false;

            var d = c - '0';
            if (doubleDigit)
            {
                d *= 2;
                if (d > 9)
                    d -= 9;
            }

            sum += d;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}

/// <summary>PAN'dan BIN (ilk 6 hane) çıkarır (denetim/gösterim; ödeme akışında resolve girdisi).</summary>
public static class BinExtractor
{
    public static string Extract(string pan)
    {
        var digits = pan.Trim();
        return digits.Length >= 6 ? digits[..6] : digits;
    }
}

/// <summary>PAN son 4 hanesi (denetim/gösterim).</summary>
public static class Last4Extractor
{
    public static string Extract(string pan)
    {
        var digits = pan.Trim();
        return digits.Length >= 4 ? digits[^4..] : digits;
    }
}

/// <summary>
/// PAN prefix'inden marka: Visa <c>4</c>, Amex <c>34/37</c>, Troy <c>9792</c>, Mastercard <c>51–55</c>
/// veya <c>2221–2720</c>; aksi <see cref="CardBrand.Unknown"/>. Saf, deterministik.
/// </summary>
public static class BrandDetector
{
    public static CardBrand Detect(string pan)
    {
        var digits = pan.Trim();
        if (digits.Length < 2)
            return CardBrand.Unknown;

        if (digits[0] == '4')
            return CardBrand.Visa;

        var two = int.Parse(digits[..2]);
        if (two == 34 || two == 37)
            return CardBrand.Amex;

        if (digits.Length >= 4 && digits[..4] == "9792")
            return CardBrand.Troy;

        if (two >= 51 && two <= 55)
            return CardBrand.MasterCard;

        if (digits.Length >= 4)
        {
            var four = int.Parse(digits[..4]);
            if (four >= 2221 && four <= 2720)
                return CardBrand.MasterCard;
        }

        return CardBrand.Unknown;
    }
}
