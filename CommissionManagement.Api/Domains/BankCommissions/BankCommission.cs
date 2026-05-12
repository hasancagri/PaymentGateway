namespace CommissionManagement.Api.Domains.BankCommissions;

public sealed class BankCommission : AggregateRoot
{
    public Guid BankId { get; private set; }
    public CommissionCriteria Criteria { get; private set; }
    public CommissionRate Rate { get; private set; }

    private BankCommission()
    {
    }

    public static BankCommission Create(
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

    public void UpdateRate(CommissionRate newRate) => Rate = newRate;
}