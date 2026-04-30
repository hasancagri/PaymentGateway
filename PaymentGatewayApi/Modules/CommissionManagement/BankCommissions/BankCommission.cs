using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Events;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions;

public sealed class BankCommission : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public BankCommissionId Id { get; private set; }
    public Guid BankId { get; private set; } // Cross-BC reference
    public CommissionCriteria Criteria { get; private set; }
    public CommissionRate Rate { get; private set; }

    // ── Audit ─────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

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
            Id = BankCommissionId.New(),
            BankId = bankId,
            Criteria = criteria,
            Rate = rate,
            CreatedAt = DateTime.UtcNow
        };

        commission.RaiseDomainEvent(new BankCommissionDefined(
            Guid.NewGuid(), DateTime.UtcNow,
            commission.Id.Value,
            bankId,
            criteria.CardBrand.ToString(),
            criteria.CardType.ToString(),
            criteria.TransactionRegion.ToString(),
            rate.Value));

        return commission;
    }

    // ── Update ────────────────────────────────────────────
    public void UpdateRate(CommissionRate newRate)
    {
        var old = Rate;
        Rate = newRate;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new BankCommissionUpdated(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, old.Value, newRate.Value));
    }
}