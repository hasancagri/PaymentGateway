namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

public class BankRouteSummary
{
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid BankId { get; private set; }
    public string BankName { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public decimal BankRate { get; private set; }
    public decimal MerchantRate { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private BankRouteSummary() { }

    public static BankRouteSummary Create(Guid id) => new() { Id = id };

    public void Sync(Guid merchantId, Guid bankId, string bankName, string currency, decimal bankRate, decimal merchantRate)
    {
        MerchantId = merchantId;
        BankId = bankId;
        BankName = bankName;
        Currency = currency;
        BankRate = bankRate;
        MerchantRate = merchantRate;
    }
}