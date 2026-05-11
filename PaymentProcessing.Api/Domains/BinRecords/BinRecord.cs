namespace PaymentProcessing.Api.PaymentProcessing.BinRecords;

public sealed class BinRecord : AggregateRoot
{
    public string BinStart { get; init; }
    public string BinEnd { get; init; }
    public long BinEightStart { get; init; }
    public long BinEightEnd { get; init; }
    public string CardBrand { get; init; }
    public string CardDci { get; init; }
    /// <summary>
    /// 1 = Consumer , 2 = Commercial , 3 = All , 4 = Other
    /// </summary>
    public string CardProductType { get; init; }
    public string CardProgram { get; init; }
    public string BinCountry { get; init; }
    public string BinRegion { get; init; }
    public string BinEuroZone { get; init; }
    public string MemberId { get; init; }
    public string MemberName { get; init; }
    public string MemberCountry { get; init; }
    public string MemberRegion { get; init; }
    public string MemberEuroZone { get; init; }

    private BinRecord() { }

    public static ResultDomain<BinRecord> Create(
        string binStart, string binEnd,
        string cardBrand, string cardProductType,
        string binCountry, string binRegion)
    {
        var errors = new List<MessageItem>();

        if (string.IsNullOrWhiteSpace(binStart) || binStart.Length < 6 || binStart.Length > 8 || !binStart.All(char.IsDigit))
            errors.Add(new MessageItem { Code = "BinRecord.InvalidBinStart" });

        if (string.IsNullOrWhiteSpace(binEnd) || binEnd.Length < 6 || binEnd.Length > 8 || !binEnd.All(char.IsDigit))
            errors.Add(new MessageItem { Code = "BinRecord.InvalidBinEnd" });

        if (string.IsNullOrWhiteSpace(cardBrand))
            errors.Add(new MessageItem { Code = "BinRecord.CardBrandRequired" });

        if (errors.Count == 0 && string.Compare(binStart, binEnd, StringComparison.Ordinal) > 0)
            errors.Add(new MessageItem { Code = "BinRecord.StartAfterEnd" });

        if (errors.Count > 0) return ResultDomain<BinRecord>.Error(errors);

        return ResultDomain<BinRecord>.Ok(new BinRecord
        {
            BinStart = binStart,
            BinEnd = binEnd,
            BinEightStart = long.Parse(binStart.PadRight(8, '0')),
            BinEightEnd = long.Parse(binEnd.PadRight(8, '9')),
            CardBrand = cardBrand,
            CardProductType = cardProductType,
            BinCountry = binCountry,
            BinRegion = binRegion
        });
    }
}