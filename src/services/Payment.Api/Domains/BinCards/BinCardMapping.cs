namespace Payment.Api.Domains.BinCards;

/// <summary>
/// Sınır çevirisi: taşınabilir int kodlar (seed JSON / import DTO — CP.VPOS enum int değerleriyle
/// birebir) → <see cref="BinCard"/> + domain enum'ları. Tanınmayan marka/program değeri →
/// <c>Unknown</c> (çökmez). CP.VPOS tipi domain sınırını GEÇMEZ (yalnız int kodları taşınır).
/// </summary>
public static class BinCardMapping
{
    /// <summary>Taşınabilir int kodlardan (seed JSON / import DTO) domain document'ine.</summary>
    public static BinCard FromCodes(string binNumber, string? bankCode, int cardType, int cardBrand, int cardProgram, bool commercial) => new()
    {
        BinNumber = binNumber,
        BankCode = bankCode ?? string.Empty,
        CardType = MapCardType(cardType),
        CardBrand = MapCardBrand(cardBrand),
        CardProgram = MapCardProgram(cardProgram),
        Commercial = commercial
    };

    public static CardType MapCardType(int value) =>
        value == (int)CardType.Credit ? CardType.Credit : CardType.Debit;

    public static CardBrand MapCardBrand(int value) =>
        Enum.IsDefined(typeof(CardBrand), value) ? (CardBrand)value : CardBrand.Unknown;

    public static CardProgram MapCardProgram(int value) =>
        Enum.IsDefined(typeof(CardProgram), value) ? (CardProgram)value : CardProgram.Unknown;
}