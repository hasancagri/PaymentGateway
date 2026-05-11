namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

public class MerchantBankSummary
{
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid BankId { get; private set; }
    public string BankName { get; private set; } = string.Empty;
    public string? IcaMemberId { get; private set; }
    public IReadOnlyCollection<string> SupportedCurrencies { get; private set; } = [];
    public bool IsActive { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantBankSummary() { }

    public static MerchantBankSummary Create(Guid id) => new() { Id = id };

    public void Sync(Guid merchantId, Guid bankId, string bankName, string? icaMemberId,
        IReadOnlyCollection<string> supportedCurrencies, bool isActive)
    {
        MerchantId = merchantId;
        BankId = bankId;
        BankName = bankName;
        IcaMemberId = icaMemberId;
        SupportedCurrencies = supportedCurrencies;
        IsActive = isActive;
    }
}