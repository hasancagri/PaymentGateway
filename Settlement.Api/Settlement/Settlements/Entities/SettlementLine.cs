
namespace PaymentGatewayApi.Modules.Settlement.Settlements.Entities;

public sealed class SettlementLine : BaseModel
{
    public Guid TransactionId { get; private set; } // Cross-BC reference
    public Money GrossAmount { get; private set; }
    public Money CommissionAmount { get; private set; }
    public Money NetAmount { get; private set; }

    private SettlementLine()
    {
    } // EF Core

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