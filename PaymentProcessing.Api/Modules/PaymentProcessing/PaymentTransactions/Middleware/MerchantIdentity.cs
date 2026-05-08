namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Middleware;

public record MerchantIdentity(
    Guid MerchantId,
    string MerchantName
);