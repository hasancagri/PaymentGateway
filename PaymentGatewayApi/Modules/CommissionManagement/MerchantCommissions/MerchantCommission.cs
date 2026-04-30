using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;
using PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Events;
using PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.ValueObjects;

namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions;

public sealed class MerchantCommission : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public MerchantCommissionId Id { get; private set; }
    public Guid MerchantId { get; private set; } // Cross-BC reference
    public CommissionCriteria Criteria { get; private set; }
    public CommissionRate Rate { get; private set; }
    public Guid BankCommissionId { get; private set; } // Reference to validate invariant

    // ── Audit ─────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private MerchantCommission()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    /// <summary>
    /// Domain Invariant: merchantRate must always be greater than bankRate.
    /// </summary>
    public static MerchantCommission Define(
        Guid merchantId,
        Guid bankCommissionId,
        CommissionCriteria criteria,
        CommissionRate merchantRate,
        CommissionRate bankRate)
    {
        EnforceRateInvariant(merchantRate, bankRate);

        var commission = new MerchantCommission
        {
            Id = MerchantCommissionId.New(),
            MerchantId = merchantId,
            BankCommissionId = bankCommissionId,
            Criteria = criteria,
            Rate = merchantRate,
            CreatedAt = DateTime.UtcNow
        };

        commission.RaiseDomainEvent(new MerchantCommissionDefined(
            Guid.NewGuid(), DateTime.UtcNow,
            commission.Id.Value,
            merchantId,
            criteria.CardBrand.ToString(),
            criteria.CardType.ToString(),
            criteria.TransactionRegion.ToString(),
            merchantRate.Value));

        return commission;
    }

    // ── Update ────────────────────────────────────────────
    public void UpdateRate(CommissionRate newRate, CommissionRate currentBankRate)
    {
        EnforceRateInvariant(newRate, currentBankRate);

        var old = Rate;
        Rate = newRate;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new MerchantCommissionUpdated(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, old.Value, newRate.Value));
    }

    // ── Invariants ────────────────────────────────────────
    private static void EnforceRateInvariant(CommissionRate merchantRate, CommissionRate bankRate)
    {
        if (merchantRate.Value <= bankRate.Value)
            throw new DomainException(
                $"Merchant commission rate ({merchantRate.Value}%) must be greater " +
                $"than bank commission rate ({bankRate.Value}%).");
    }
}