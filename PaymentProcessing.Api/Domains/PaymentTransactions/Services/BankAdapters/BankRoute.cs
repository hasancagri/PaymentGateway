namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Services.BankAdapters;

public record BankRoute(
    Guid BankId,
    string BankName,
    decimal BankRate,
    decimal MerchantRate
);