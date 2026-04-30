namespace PaymentGatewayApi.Modules.Settlement.Settlements.Events;

public sealed record SettlementStarted(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     SettlementId,
    Guid     MerchantId,
    string   PeriodStart,
    string   PeriodEnd
) : IDomainEvent;

public sealed record SettlementLineAdded(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     SettlementId,
    Guid     TransactionId,
    decimal  NetAmount,
    string   Currency
) : IDomainEvent;

public sealed record SettlementCompleted(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     SettlementId,
    Guid     MerchantId,
    decimal  TotalNetAmount,
    string   Currency
) : IDomainEvent;

public sealed record SettlementFailed(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     SettlementId,
    string   Reason
) : IDomainEvent;
