namespace PaymentGateway.SharedContracts.PaymentEvents;

public sealed record PaymentApprovedIntegration(Guid TransactionId, Guid MerchantId, string OrderId, decimal Amount, string Currency, decimal MerchantAmount, DateTime OccurredOn);
public sealed record PaymentDeclinedIntegration(Guid TransactionId, Guid MerchantId, string OrderId, string ResultCode, DateTime OccurredOn);
public sealed record PaymentFailedIntegration(Guid TransactionId, Guid MerchantId, string OrderId, string Reason, DateTime OccurredOn);