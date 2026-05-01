using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions;

public sealed class BankCommission : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public Guid BankId { get; private set; } // Cross-BC reference
    public CommissionCriteria Criteria { get; private set; }
    public CommissionRate Rate { get; private set; }
    
    private BankCommission()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static BankCommission Define(
        Guid bankId,
        CommissionCriteria criteria,
        CommissionRate rate)
    {
        var commission = new BankCommission
        {
            BankId = bankId,
            Criteria = criteria,
            Rate = rate
        };

        return commission;
    }

    // ── Update ────────────────────────────────────────────
    public void UpdateRate(CommissionRate newRate)
    {
        var old = Rate;
        Rate = newRate;
    }
}