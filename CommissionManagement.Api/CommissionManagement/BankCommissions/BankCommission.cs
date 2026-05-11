using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions;

public sealed class BankCommission : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public Guid BankId { get; init; }
    public CommissionCriteria Criteria { get; init; }
    private CommissionRate _rate;
    public CommissionRate Rate { get => _rate; init => _rate = value; }

    private BankCommission() { }

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
    public void UpdateRate(CommissionRate newRate) => _rate = newRate;
}