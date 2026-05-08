namespace PaymentGatewayApi.Modules.PaymentProcessing.BinRecords;

public sealed class BinRecord : AggregateRoot
{
    public string BinStart { get; private set; }
    public string BinEnd { get; private set; }
    public long BinEightStart { get; private set; }
    public long BinEightEnd { get; private set; }
    public string CardBrand { get; private set; }
    public string CardDci { get; private set; }
    /// <summary>
    /// 1 = Consumer , 2 = Commercial , 3 = All , 4 = Other
    /// </summary>
    public string CardProductType { get; private set; }
    public string CardProgram { get; private set; }
    public string BinCountry { get; private set; }
    public string BinRegion { get; private set; }
    public string BinEuroZone { get; private set; }
    public string MemberId { get; private set; }
    public string MemberName { get; private set; }
    public string MemberCountry { get; private set; }
    public string MemberRegion { get; private set; }
    public string MemberEuroZone { get; private set; }

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