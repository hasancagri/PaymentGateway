
namespace PaymentGatewayApi.Modules.Settlement.Settlements.Entities;

public sealed class SettlementLine
{
    public Guid Id { get; private set; }
    public Guid TransactionId { get; private set; } // Cross-BC reference
    public Money GrossAmount { get; private set; }
    public Money CommissionAmount { get; private set; }
    public Money NetAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SettlementLine()
    {
    } // EF Core

    internal static SettlementLine Create(
        Guid transactionId,
        Money grossAmount,
        Money commissionAmount,
        Money netAmount) => new()
    {
        Id = Guid.NewGuid(),
        TransactionId = transactionId,
        GrossAmount = grossAmount,
        CommissionAmount = commissionAmount,
        NetAmount = netAmount,
        CreatedAt = DateTime.UtcNow
    };
}