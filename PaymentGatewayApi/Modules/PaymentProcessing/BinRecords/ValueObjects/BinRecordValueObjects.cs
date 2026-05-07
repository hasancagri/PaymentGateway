namespace PaymentGatewayApi.Modules.PaymentProcessing.BinRecords.ValueObjects;

public sealed record BinRange
{
    public string Start { get; }
    public string End { get; }

    [JsonConstructor]
    private BinRange(string start, string end)
    {
        Start = start;
        End = end;
    }

    public static ResultDomain<BinRange> Create(string start, string end)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(start) || start.Length < 6 || !start.All(char.IsDigit))
            errors.Add(new MessageItem { Code = "BinRange.InvalidStart" });
        if (string.IsNullOrWhiteSpace(end) || end.Length < 6 || !end.All(char.IsDigit))
            errors.Add(new MessageItem { Code = "BinRange.InvalidEnd" });
        if (errors.Count == 0 && string.Compare(start, end, StringComparison.Ordinal) > 0)
            errors.Add(new MessageItem { Code = "BinRange.StartAfterEnd" });
        if (errors.Count > 0) return ResultDomain<BinRange>.Error(errors);
        return ResultDomain<BinRange>.Ok(new BinRange(start, end));
    }

    public static BinRange FromPersistence(string start, string end) => new(start, end);

    public bool Contains(string bin) =>
        string.Compare(bin, Start, StringComparison.Ordinal) >= 0 &&
        string.Compare(bin, End, StringComparison.Ordinal) <= 0;
}

public sealed record BinCardInfo
{
    public string CardBrand { get; }
    public string CardType { get; }
    public string IssuingCountry { get; }
    public string Region { get; }

    [JsonConstructor]
    private BinCardInfo(string cardBrand, string cardType, string issuingCountry, string region)
    {
        CardBrand = cardBrand;
        CardType = cardType;
        IssuingCountry = issuingCountry;
        Region = region;
    }

    public static ResultDomain<BinCardInfo> Create(
        string cardBrand, string cardType, string issuingCountry, string region)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(cardBrand))
            errors.Add(new MessageItem { Code = "BinCardInfo.CardBrandEmpty" });
        if (string.IsNullOrWhiteSpace(cardType))
            errors.Add(new MessageItem { Code = "BinCardInfo.CardTypeEmpty" });
        if (string.IsNullOrWhiteSpace(issuingCountry))
            errors.Add(new MessageItem { Code = "BinCardInfo.IssuingCountryEmpty" });
        if (errors.Count > 0) return ResultDomain<BinCardInfo>.Error(errors);

        return ResultDomain<BinCardInfo>.Ok(new BinCardInfo(
            cardBrand.Trim(), cardType.Trim(),
            issuingCountry.Trim().ToUpperInvariant(), region.Trim()));
    }

    public static BinCardInfo FromPersistence(string cardBrand, string cardType, string issuingCountry, string region)
        => new(cardBrand, cardType, issuingCountry, region);
}