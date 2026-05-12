namespace CommissionManagement.Api.Domains.MerchantCommissions;

public sealed class MerchantCommission : AggregateRoot
{
    public Guid MerchantId { get; private set; }
    public CommissionCriteria Criteria { get; private set; }
    public Guid BankCommissionId { get; private set; }
    public CommissionRate Rate { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantCommission()
    {
    }

    public static ResultDomain<MerchantCommission> Create(
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

    public ResultDomain UpdateRate(CommissionRate newRate, CommissionRate currentBankRate)
    {
        var invariant = EnforceRateInvariant(newRate, currentBankRate);
        if (!invariant.IsSuccess) return invariant;

        Rate = newRate;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

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