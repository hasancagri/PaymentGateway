using PaymentGatewayApi.Modules.BankIntegration.BinRecords.ValueObjects;

namespace PaymentGatewayApi.Modules.BankIntegration.BinRecords;

public sealed class BinRecord : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public BinRange BinRange { get; private set; }
    public BinCardInfo CardInfo { get; private set; }


    private BinRecord()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static BinRecord Create(BinRange binRange, BinCardInfo cardInfo)
    {
        var record = new BinRecord
        {
            BinRange = binRange,
            CardInfo = cardInfo,
        };

        return record;
    }

    // ── Query ─────────────────────────────────────────────
    public bool Matches(string bin) => BinRange.Contains(bin);
}