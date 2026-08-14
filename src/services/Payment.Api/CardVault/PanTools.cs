using Payment.Api.Domains.StoredCards;

namespace Payment.Api.CardVault;

// 032 (Model A): PAN artık iyzico'da saklanır, gateway'de Luhn/AES yok. Bu dosya yalnız gösterim
// türetimlerini + sağlayıcı marka eşlemesini tutar (saf, altyapı). LuhnValidator/IPanProtector 032'de
// SİLİNDİ (iyzico doğrular). Bin/Last4/BrandDetector FALLBACK: iyzico gösterim alanını vermezse.

/// <summary>iyzico <c>CardAssociation</c> string'ini BC-içi <see cref="CardBrand"/>'e eşler.</summary>
public static class CardAssociationMapper
{
    public static CardBrand Map(string? association) => (association ?? string.Empty).ToUpperInvariant() switch
    {
        "VISA" => CardBrand.Visa,
        "MASTER_CARD" => CardBrand.MasterCard,
        "AMERICAN_EXPRESS" => CardBrand.Amex,
        "TROY" => CardBrand.Troy,
        _ => CardBrand.Unknown
    };
}

/// <summary>PAN'dan BIN (ilk 6 hane) çıkarır (fallback gösterim; iyzico normalde döndürür).</summary>
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
