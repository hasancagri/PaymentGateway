namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Events;

public sealed record PaymentInitiated(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     TransactionId,
    Guid     MerchantId,
    string   OrderId,
    decimal  Amount,
    string   Currency
) : IDomainEvent;

public sealed record BankSelected(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     TransactionId,
    Guid     BankId
) : IDomainEvent;

public sealed record PaymentApproved(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     TransactionId,
    Guid     MerchantId,
    string   OrderId,
    decimal  Amount,
    string   Currency,
    decimal  MerchantCommissionAmount,
    decimal  BankCommissionAmount,
    decimal  NetAmount
) : IDomainEvent;

public sealed record PaymentDeclined(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     TransactionId,
    string   OrderId,
    string   BankResponseCode,
    string   BankMessage
) : IDomainEvent;

public sealed record PaymentFailed(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     TransactionId,
    string   Reason
) : IDomainEvent;

public sealed record TransactionSettled(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     TransactionId,
    Guid     MerchantId,
    Guid     SettlementId
) : IDomainEvent;