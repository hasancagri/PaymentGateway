namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Events;

public sealed record BankCommissionDefined(
    Guid EventId,
    DateTime OccurredOn,
    Guid BankCommissionId,
    Guid BankId,
    string CardBrand,
    string CardType,
    string TransactionRegion,
    decimal Rate
);

public sealed record BankCommissionUpdated(
    Guid EventId,
    DateTime OccurredOn,
    Guid BankCommissionId,
    decimal OldRate,
    decimal NewRate
);