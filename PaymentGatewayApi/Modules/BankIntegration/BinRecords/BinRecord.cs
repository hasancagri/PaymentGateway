using PaymentGatewayApi.Modules.BankIntegration.BinRecords.Events;
using PaymentGatewayApi.Modules.BankIntegration.BinRecords.ValueObjects;

namespace PaymentGatewayApi.Modules.BankIntegration.BinRecords;

public sealed class BinRecord : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public BinRecordId Id { get; private set; }
    public BinRange BinRange { get; private set; }
    public BinCardInfo CardInfo { get; private set; }

    // ── Audit ─────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private BinRecord()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static BinRecord Create(BinRange binRange, BinCardInfo cardInfo)
    {
        var record = new BinRecord
        {
            Id = BinRecordId.New(),
            BinRange = binRange,
            CardInfo = cardInfo,
            CreatedAt = DateTime.UtcNow
        };

        record.RaiseDomainEvent(new BinRecordCreated(
            Guid.NewGuid(), DateTime.UtcNow,
            record.Id.Value,
            binRange.Start,
            binRange.End,
            cardInfo.CardBrand,
            cardInfo.CardType));

        return record;
    }

    // ── Query ─────────────────────────────────────────────
    public bool Matches(string bin) => BinRange.Contains(bin);
}