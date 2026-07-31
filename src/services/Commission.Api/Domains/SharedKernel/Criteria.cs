namespace Commission.Api.Domains.SharedKernel;

/// <summary>
/// Komisyonun uygulandığı kombinasyon: kart markası × kart tipi × işlem bölgesi × taksit sayısı.
/// Değer nesnesi (record → değer eşitliği; benzersizlik bu dörtlüye dayanır). Taksit ekseni
/// zorunlu çünkü banka oranı taksitle değişir; invariant taksit-taksit eşleşir.
/// </summary>
public record Criteria
{
    public CardBrand CardBrand { get; private set; }
    public CardType CardType { get; private set; }
    public TransactionRegion TransactionRegion { get; private set; }
    public int InstallmentCount { get; private set; }

    private Criteria()
    {
    }

    public static ResultDomain<Criteria> Create(
        CardBrand cardBrand,
        CardType cardType,
        TransactionRegion transactionRegion,
        int installmentCount)
    {
        if (installmentCount < 1)
        {
            return ResultDomain<Criteria>.Error(new MessageItem
            {
                Property = nameof(InstallmentCount),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        return ResultDomain<Criteria>.Ok(new Criteria
        {
            CardBrand = cardBrand,
            CardType = cardType,
            TransactionRegion = transactionRegion,
            InstallmentCount = installmentCount
        });
    }

    /// <summary>API kod stringlerinden Criteria kurar (enum parse + taksit doğrulama).</summary>
    public static ResultDomain<Criteria> FromCodes(
        string cardBrand,
        string cardType,
        string transactionRegion,
        int installmentCount)
    {
        if (!TryParse<CardBrand>(cardBrand, out var brand))
            return InvalidEnum(nameof(CardBrand));
        if (!TryParse<CardType>(cardType, out var type))
            return InvalidEnum(nameof(CardType));
        if (!TryParse<TransactionRegion>(transactionRegion, out var region))
            return InvalidEnum(nameof(TransactionRegion));

        return Create(brand, type, region, installmentCount);
    }

    private static bool TryParse<TEnum>(string value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) &&
               Enum.TryParse(value, ignoreCase: true, out result) &&
               Enum.IsDefined(result);
    }

    private static ResultDomain<Criteria> InvalidEnum(string property) =>
        ResultDomain<Criteria>.Error(new MessageItem
        {
            Property = property,
            Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_ENUM_TYPE
        });
}