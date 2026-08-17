namespace Payment.Api.Domains.StoredCards.ValueObjects;

/// <summary>
/// Tokenize-anı ham kart bilgisi — değer nesnesi (035). Model A (032): PAN iyzico'ya BİR KEZ gider,
/// gateway saklamaz → bu VO transient (kalıcı StoredCard alanı DEĞİL). Luhn/AES gateway'de YOK (iyzico
/// doğrular) — yapısal doğrulama yalnız expiry (MM/yy) + boş-değil. Handler expiry parse'ı + rakam
/// süzmeyi VO'ya kapsüller; handler <c>TokenizeCard</c> slice'ının nested wire CardInfo'suna map'ler.
/// </summary>
public sealed class CardInformation
{
    private CardInformation()
    {
    }

    /// <summary>Yalnız rakam (süzülmüş PAN); transient — saklanmaz.</summary>
    public string CardNumber { get; private init; } = string.Empty;

    /// <summary>2 hane ay.</summary>
    public string ExpireMonth { get; private init; } = string.Empty;

    /// <summary>4 hane yıl (20yy).</summary>
    public string ExpireYear { get; private init; } = string.Empty;

    public string CardHolderName { get; private init; } = string.Empty;

    /// <summary>Ham "MM/yy" (StoredCard kaydına gösterim için — mevcut sözleşme).</summary>
    public string RawExpiry { get; private init; } = string.Empty;

    /// <summary>
    /// Ham karttan VO üretir. Expiry "MM/yy" ayrıştırılır (ay 1-2 hane, yıl 2 hane → 20yy); kart no
    /// rakamları süzülür (boş olamaz); sahip boş olamaz. Luhn YOK (Model A — iyzico doğrular).
    /// Geçersizse <c>Error</c>.
    /// </summary>
    public static ResultDomain<CardInformation> Create(string pan, string expiry, string holderName)
    {
        var parts = (expiry ?? string.Empty).Split('/');
        if (parts.Length != 2 || parts[0].Trim().Length is < 1 or > 2 || parts[1].Trim().Length != 2)
            return ResultDomain<CardInformation>.Error(new MessageItem
            { Property = nameof(RawExpiry), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });

        var digits = new string((pan ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return ResultDomain<CardInformation>.Error(new MessageItem
            { Property = nameof(CardNumber), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });

        if (string.IsNullOrWhiteSpace(holderName))
            return ResultDomain<CardInformation>.Error(new MessageItem
            { Property = nameof(CardHolderName), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        return ResultDomain<CardInformation>.Ok(new CardInformation
        {
            CardNumber = digits,
            ExpireMonth = parts[0].Trim().PadLeft(2, '0'),
            ExpireYear = "20" + parts[1].Trim(),
            CardHolderName = holderName.Trim(),
            RawExpiry = expiry!.Trim()
        });
    }
}
