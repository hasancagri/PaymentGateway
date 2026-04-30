namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Events;

public sealed record MerchantBalanceCredited(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     MerchantBalanceId,
    Guid     MerchantId,
    decimal  Amount,
    string   Currency,
    Guid?    ReferenceId
) : IDomainEvent;

public sealed record MerchantBalanceDebited(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     MerchantBalanceId,
    Guid     MerchantId,
    decimal  Amount,
    string   Currency,
    Guid?    ReferenceId
) : IDomainEvent;

public sealed record WithdrawalRequested(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     MerchantBalanceId,
    Guid     MerchantId,
    Guid     WithdrawalId,
    decimal  Amount,
    string   Currency,
    string   TargetIban
) : IDomainEvent;

public sealed record WithdrawalApproved(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     MerchantId,
    Guid     WithdrawalId
) : IDomainEvent;

public sealed record WithdrawalRejected(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     MerchantId,
    Guid     WithdrawalId,
    string   Reason
) : IDomainEvent;

public sealed record WithdrawalProcessed(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     MerchantId,
    Guid     WithdrawalId
) : IDomainEvent;
