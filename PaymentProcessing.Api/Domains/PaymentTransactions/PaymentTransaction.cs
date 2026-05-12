namespace PaymentProcessing.Api.Domains.PaymentTransactions;

public class PaymentTransaction
{
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public string OrderId { get; private set; } = null!;
    public TransactionType Type { get; private set; }
    public TransactionStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string EncryptedCardNumber { get; private set; } = null!;
    public string CardHolderName { get; private set; } = null!;
    public string ExpiryMonth { get; private set; } = null!;
    public string ExpiryYear { get; private set; } = null!;
    public string CardHolderIp { get; private set; } = null!;
    public Guid SelectedBankId { get; private set; }
    public decimal BankRate { get; private set; }
    public decimal MerchantRate { get; private set; }
    public decimal BankCommissionAmount { get; private set; }
    public decimal MerchantCommissionAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string? BankTransactionId { get; private set; }
    public string? BankResultCode { get; private set; }
    public string? BankMessage { get; private set; }
    public int InstallmentCount { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private PaymentTransaction() { }

    public void Apply(PaymentInitiated e)
    {
        Id = e.TransactionId;
        MerchantId = e.MerchantId;
        OrderId = e.OrderId;
        Type = TransactionType.Auth;
        Status = TransactionStatus.Pending;
        Amount = e.Amount;
        Currency = e.Currency;
        EncryptedCardNumber = e.EncryptedCardNumber;
        CardHolderName = e.CardHolderName;
        ExpiryMonth = e.ExpiryMonth;
        ExpiryYear = e.ExpiryYear;
        CardHolderIp = e.CardHolderIp;
        SelectedBankId = e.SelectedBankId;
        BankRate = e.BankRate;
        MerchantRate = e.MerchantRate;
        InstallmentCount = e.InstallmentCount;
    }

    public void Apply(PaymentApproved e)
    {
        Status = TransactionStatus.Approved;
        BankCommissionAmount = e.BankCommissionAmount;
        MerchantCommissionAmount = e.MerchantCommissionAmount;
        NetAmount = e.NetAmount;
        BankTransactionId = e.BankTransactionId;
        BankResultCode = e.ResultCode;
    }

    public void Apply(PaymentDeclined e)
    {
        Status = TransactionStatus.Declined;
        BankResultCode = e.BankResponseCode;
        BankMessage = e.BankMessage;
    }

    public void Apply(PaymentFailed e)
    {
        Status = TransactionStatus.Failed;
        BankResultCode = "99";
        BankMessage = e.Reason;
    }
}