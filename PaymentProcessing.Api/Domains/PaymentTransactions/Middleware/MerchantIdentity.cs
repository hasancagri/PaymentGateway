namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Middleware;

public record MerchantIdentity(
    Guid MerchantId,
    string MerchantName
);