namespace PaymentGatewayApi.Modules.BankIntegration.Banks.Events;

public sealed record BankConfigured(
    Guid EventId,
    DateTime OccuredOn,
    Guid BankId,
    string BankName
) : IDomainEvent;

public sealed record BankUpdated(
    Guid EventId,
    DateTime OccuredOn,
    Guid BankId
) : IDomainEvent;

public sealed record BankStatusChanged(
    Guid EventId,
    DateTime OccuredOn,
    Guid BankId,
    string OldStatus,
    string NewStatus
) : IDomainEvent;