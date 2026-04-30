namespace PaymentGatewayApi.Modules.BankIntegration.BinRecords.ValueObjects;

public sealed record BinRecordId(Guid Value)
{
    public static BinRecordId New() => new(Guid.NewGuid());
    public static BinRecordId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public sealed record BinRange
{
    public string Start { get; }
    public string End { get; }

    public BinRange(string start, string end)
    {
        if (string.IsNullOrWhiteSpace(start) || start.Length < 6 || !start.All(char.IsDigit))
            throw new DomainException("BIN start must be at least 6 digits.");
        if (string.IsNullOrWhiteSpace(end) || end.Length < 6 || !end.All(char.IsDigit))
            throw new DomainException("BIN end must be at least 6 digits.");
        if (string.Compare(start, end, StringComparison.Ordinal) > 0)
            throw new DomainException("BIN start must be less than or equal to BIN end.");

        Start = start;
        End = end;
    }

    public bool Contains(string bin) =>
        string.Compare(bin, Start, StringComparison.Ordinal) >= 0 &&
        string.Compare(bin, End, StringComparison.Ordinal) <= 0;
}

public sealed record BinCardInfo
{
    public string CardBrand { get; }
    public string CardType { get; }
    public string IssuingCountry { get; } // ISO 3166-1 alpha-2
    public string Region { get; } // Domestic / International

    public BinCardInfo(
        string cardBrand,
        string cardType,
        string issuingCountry,
        string region)
    {
        if (string.IsNullOrWhiteSpace(cardBrand))
            throw new DomainException("Card brand cannot be empty.");
        if (string.IsNullOrWhiteSpace(cardType))
            throw new DomainException("Card type cannot be empty.");
        if (string.IsNullOrWhiteSpace(issuingCountry))
            throw new DomainException("Issuing country cannot be empty.");

        CardBrand = cardBrand.Trim();
        CardType = cardType.Trim();
        IssuingCountry = issuingCountry.Trim().ToUpperInvariant();
        Region = region.Trim();
    }
}