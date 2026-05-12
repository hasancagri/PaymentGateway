namespace PaymentProcessing.Api.Domains.PaymentTransactions.Services.BankAdapters;

public record BankAuthResult(
    bool IsApproved,
    string? BankTransactionId,
    string ResultCode,
    string? Message
);