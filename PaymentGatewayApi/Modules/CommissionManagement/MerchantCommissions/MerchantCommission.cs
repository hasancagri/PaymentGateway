using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;

namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions;

public sealed class MerchantCommission : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public Guid MerchantId { get; private set; } // Cross-BC reference
    public CommissionCriteria Criteria { get; private set; }
    public CommissionRate Rate { get; private set; }
    public Guid BankCommissionId { get; private set; } // Reference to validate invariant


    private MerchantCommission()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    /// <summary>
    /// Domain Invariant: merchantRate must always be greater than bankRate.
    /// </summary>
    public static ResultDomain<MerchantCommission> Define(
        Guid merchantId,
        Guid bankCommissionId,
        CommissionCriteria criteria,
        CommissionRate merchantRate,
        CommissionRate bankRate)
    {
        var invariant = EnforceRateInvariant(merchantRate, bankRate);
        if (!invariant.IsSuccess) return ResultDomain<MerchantCommission>.Error(invariant.Messages!);

        var commission = new MerchantCommission
        {
            MerchantId = merchantId,
            BankCommissionId = bankCommissionId,
            Criteria = criteria,
            Rate = merchantRate,
        };

        return ResultDomain<MerchantCommission>.Ok(commission);
    }

    // ── Update ────────────────────────────────────────────
    public ResultDomain UpdateRate(CommissionRate newRate, CommissionRate currentBankRate)
    {
        var invariant = EnforceRateInvariant(newRate, currentBankRate);
        if (!invariant.IsSuccess) return invariant;

        Rate = newRate;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    // ── Invariants ────────────────────────────────────────
    private static ResultDomain EnforceRateInvariant(CommissionRate merchantRate, CommissionRate bankRate)
    {
        if (merchantRate.Value <= bankRate.Value)
        {
            return ResultDomain.Error(new MessageItem()
            {
                Code = "12"
            });
        }

        return ResultDomain.Ok();
    }
}