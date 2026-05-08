namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Events;

public sealed record PaymentInitiated(
    Guid TransactionId,
    Guid MerchantId,
    string OrderId,
    decimal Amount,
    string Currency,
    string EncryptedCardNumber,
    string CardHolderName,
    string ExpiryMonth,
    string ExpiryYear,
    string CardHolderIp,
    Guid SelectedBankId,
    decimal BankRate,
    decimal MerchantRate,
    int InstallmentCount
);

public sealed record PaymentApproved(
    Guid TransactionId,
    Guid MerchantId,
    string OrderId,
    decimal Amount,
    string Currency,
    decimal MerchantCommissionAmount,
    decimal BankCommissionAmount,
    decimal NetAmount,
    string? BankTransactionId,
    string ResultCode
);

public sealed record PaymentDeclined(
    Guid TransactionId,
    Guid MerchantId,
    string OrderId,
    string BankResponseCode,
    string? BankMessage
);

public sealed record PaymentFailed(
    Guid TransactionId,
    Guid MerchantId,
    string OrderId,
    string Reason
);