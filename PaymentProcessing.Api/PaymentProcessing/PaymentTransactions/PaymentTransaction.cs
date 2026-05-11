using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Enums;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions;

public class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string OrderId { get; set; } = null!;
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public string EncryptedCardNumber { get; set; } = null!;
    public string CardHolderName { get; set; } = null!;
    public string ExpiryMonth { get; set; } = null!;
    public string ExpiryYear { get; set; } = null!;
    public string CardHolderIp { get; set; } = null!;
    public Guid SelectedBankId { get; set; }
    public decimal BankRate { get; set; }
    public decimal MerchantRate { get; set; }
    public decimal BankCommissionAmount { get; set; }
    public decimal MerchantCommissionAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string? BankTransactionId { get; set; }
    public string? BankResultCode { get; set; }
    public string? BankMessage { get; set; }
    public int InstallmentCount { get; set; }

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