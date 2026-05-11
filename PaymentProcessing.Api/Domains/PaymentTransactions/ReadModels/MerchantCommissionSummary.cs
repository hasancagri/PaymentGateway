namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

public class MerchantCommissionSummary
{
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid BankCommissionId { get; private set; }
    public decimal Rate { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantCommissionSummary() { }

    public static MerchantCommissionSummary Create(Guid id) => new() { Id = id };

    public void Sync(Guid merchantId, Guid bankCommissionId, decimal rate)
    {
        MerchantId = merchantId;
        BankCommissionId = bankCommissionId;
        Rate = rate;
    }

    public void UpdateRate(decimal newRate) => Rate = newRate;
}