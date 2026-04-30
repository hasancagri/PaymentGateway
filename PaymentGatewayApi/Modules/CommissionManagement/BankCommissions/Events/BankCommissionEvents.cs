namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Events;

public sealed record BankCommissionDefined(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     BankCommissionId,
    Guid     BankId,
    string   CardBrand,
    string   CardType,
    string   TransactionRegion,
    decimal  Rate
) : IDomainEvent;

public sealed record BankCommissionUpdated(
    Guid     EventId,
    DateTime OccuredOn,
    Guid     BankCommissionId,
    decimal  OldRate,
    decimal  NewRate
) : IDomainEvent;
