namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

public class BankCommissionSummary
{
    public Guid Id { get; private set; }
    public Guid BankId { get; private set; }
    public string CardBrand { get; private set; } = string.Empty;
    public string CardType { get; private set; } = string.Empty;
    public string TransactionRegion { get; private set; } = string.Empty;
    public decimal Rate { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private BankCommissionSummary() { }

    public static BankCommissionSummary Create(Guid id) => new() { Id = id };

    public void Sync(Guid bankId, string cardBrand, string cardType, string transactionRegion, decimal rate)
    {
        BankId = bankId;
        CardBrand = cardBrand;
        CardType = cardType;
        TransactionRegion = transactionRegion;
        Rate = rate;
    }

    public void UpdateRate(decimal newRate) => Rate = newRate;
}