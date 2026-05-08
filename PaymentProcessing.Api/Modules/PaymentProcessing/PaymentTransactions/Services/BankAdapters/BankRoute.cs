namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Services.BankAdapters;

public record BankRoute(
    Guid BankId,
    string BankName,
    decimal BankRate,
    decimal MerchantRate
);