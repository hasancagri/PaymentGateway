using PaymentGatewayApi.Modules.PaymentProcessing.BinRecords.ValueObjects;

namespace PaymentGatewayApi.Modules.PaymentProcessing.BinRecords;

public sealed class BinRecord : AggregateRoot
{
    public BinRange BinRange { get; private set; }
    public BinCardInfo CardInfo { get; private set; }

    private BinRecord() { } // EF Core

    public static BinRecord Create(BinRange binRange, BinCardInfo cardInfo)
    {
        return new BinRecord
        {
            BinRange = binRange,
            CardInfo = cardInfo,
        };
    }

    public string BinStart { get; set; }
    public string BinEnd { get; set; }
    public long BinEightStart { get; set; }
    public long BinEightEnd { get; set; }
    public string CardBrand { get; set; }
    public string CardDci { get; set; }
    /// <summary>
    /// 1 = Consumer , 2 = Commercial , 3 = All , 4 = Other
    /// </summary>
    public string CardProductType { get; set; }
    public string CardProgram { get; set; }
    public string BinCountry { get; set; }
    public string BinRegion { get; set; }
    public string BinEuroZone { get; set; }
    public string MemberId { get; set; }
    public string MemberName { get; set; }
    public string MemberCountry { get; set; }
    public string MemberRegion { get; set; }
    public string MemberEuroZone { get; set; }

    public bool Matches(string bin) => BinRange.Contains(bin);
}