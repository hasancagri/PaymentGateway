namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Events;

public sealed record PaymentInitiated(
    Guid EventId,
    DateTime OccurredOn,
    Guid TransactionId,
    Guid MerchantId,
    string OrderId,
    decimal Amount,
    string Currency
);

public sealed record BankSelected(
    Guid EventId,
    DateTime OccurredOn,
    Guid TransactionId,
    Guid BankId
);

public sealed record PaymentApproved(
    Guid EventId,
    DateTime OccurredOn,
    Guid TransactionId,
    Guid MerchantId,
    string OrderId,
    decimal Amount,
    string Currency,
    decimal MerchantCommissionAmount,
    decimal BankCommissionAmount,
    decimal NetAmount
);

public sealed record PaymentDeclined(
    Guid EventId,
    DateTime OccurredOn,
    Guid TransactionId,
    string OrderId,
    string BankResponseCode,
    string BankMessage
);

public sealed record PaymentFailed(
    Guid EventId,
    DateTime OccurredOn,
    Guid TransactionId,
    string Reason
);

public sealed record TransactionSettled(
    Guid EventId,
    DateTime OccurredOn,
    Guid TransactionId,
    Guid MerchantId,
    Guid SettlementId
);