namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Services.BankAdapters;

public record BankAuthResult(
    bool IsApproved,
    string? BankTransactionId,
    string ResultCode,
    string? Message
);