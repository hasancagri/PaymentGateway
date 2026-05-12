namespace PaymentProcessing.Api.Domains.PaymentTransactions.Middleware;

public record MerchantIdentity(
    Guid MerchantId,
    string MerchantName
);