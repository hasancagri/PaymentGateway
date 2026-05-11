
namespace Settlement.Api.Settlement.Settlements.Entities;

public sealed class SettlementLine : BaseModel
{
    public Guid TransactionId { get; init; }
    public Money GrossAmount { get; init; }
    public Money CommissionAmount { get; init; }
    public Money NetAmount { get; init; }

    [Newtonsoft.Json.JsonConstructor]
    private SettlementLine() { }

    internal static SettlementLine Create(
        Guid transactionId,
        Money grossAmount,
        Money commissionAmount,
        Money netAmount) => new()
    {
        TransactionId = transactionId,
        GrossAmount = grossAmount,
        CommissionAmount = commissionAmount,
        NetAmount = netAmount
    };
}