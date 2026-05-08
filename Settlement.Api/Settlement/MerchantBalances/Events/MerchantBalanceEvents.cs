namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Events;

public sealed record MerchantBalanceCredited(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantBalanceId,
    Guid MerchantId,
    decimal Amount,
    string Currency,
    Guid? ReferenceId
);

public sealed record MerchantBalanceDebited(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantBalanceId,
    Guid MerchantId,
    decimal Amount,
    string Currency,
    Guid? ReferenceId
);

public sealed record WithdrawalRequested(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantBalanceId,
    Guid MerchantId,
    Guid WithdrawalId,
    decimal Amount,
    string Currency,
    string TargetIban
);

public sealed record WithdrawalApproved(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    Guid WithdrawalId
);

public sealed record WithdrawalRejected(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    Guid WithdrawalId,
    string Reason
);

public sealed record WithdrawalProcessed(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    Guid WithdrawalId
);