using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;

namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions;

public sealed class MerchantCommission : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public Guid MerchantId { get; init; }
    public CommissionCriteria Criteria { get; init; }
    public Guid BankCommissionId { get; init; }
    private CommissionRate _rate;
    public CommissionRate Rate { get => _rate; init => _rate = value; }

    private MerchantCommission() { }

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

        _rate = newRate;
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