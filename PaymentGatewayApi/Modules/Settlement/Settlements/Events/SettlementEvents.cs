namespace PaymentGatewayApi.Modules.Settlement.Settlements.Events;

public sealed record SettlementStarted(
    Guid EventId,
    DateTime OccurredOn,
    Guid SettlementId,
    Guid MerchantId,
    string PeriodStart,
    string PeriodEnd
);

public sealed record SettlementLineAdded(
    Guid EventId,
    DateTime OccurredOn,
    Guid SettlementId,
    Guid TransactionId,
    decimal NetAmount,
    string Currency
);

public sealed record SettlementCompleted(
    Guid EventId,
    DateTime OccurredOn,
    Guid SettlementId,
    Guid MerchantId,
    decimal TotalNetAmount,
    string Currency
);

public sealed record SettlementFailed(
    Guid EventId,
    DateTime OccurredOn,
    Guid SettlementId,
    string Reason
);