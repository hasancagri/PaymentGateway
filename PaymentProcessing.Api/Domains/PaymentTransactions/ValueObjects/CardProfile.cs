namespace PaymentProcessing.Api.Domains.PaymentTransactions.ValueObjects;

public sealed record CardProfile
{
    public CardBrand CardBrand { get; init; }
    public CardType CardType { get; init; }
    public TransactionRegion TransactionRegion { get; init; }
    public string? IssuingMemberId { get; init; }

    public static ResultDomain<CardProfile> CreateFromBinRecord(BinRecord binRecord)
    {
        if (!TryMapCardBrand(binRecord.CardBrand, out var cardBrand))
            return ResultDomain<CardProfile>.Error(new MessageItem { Code = "BinRecord.UnknownCardBrand" });

        return ResultDomain<CardProfile>.Ok(new CardProfile
        {
            CardBrand = cardBrand,
            CardType = binRecord.CardProductType switch
            {
                "2" => CardType.Commercial,
                _   => CardType.Consumer
            },
            TransactionRegion = binRecord.BinCountry.ToUpperInvariant() == "TR"
                ? TransactionRegion.Domestic
                : TransactionRegion.International,
            IssuingMemberId = binRecord.MemberId
        });
    }

    private static bool TryMapCardBrand(string raw, out CardBrand cardBrand)
    {
        switch (raw.ToUpperInvariant())
        {
            case "VISA":
                cardBrand = CardBrand.Visa; return true;
            case "MASTERCARD":
            case "MC":
                cardBrand = CardBrand.Mastercard; return true;
            case "TROY":
                cardBrand = CardBrand.Troy; return true;
            case "AMEX":
            case "AMERICANEXPRESS":
                cardBrand = CardBrand.Amex; return true;
            default:
                cardBrand = default; return false;
        }
    }
}