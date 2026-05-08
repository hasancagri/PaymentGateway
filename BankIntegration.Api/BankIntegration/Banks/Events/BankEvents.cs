namespace PaymentGatewayApi.Modules.BankIntegration.Banks.Events;

public sealed record BankConfigured(
    Guid EventId,
    DateTime OccurredOn,
    Guid BankId,
    string BankName
);

public sealed record BankUpdated(
    Guid EventId,
    DateTime OccurredOn,
    Guid BankId
);

public sealed record BankStatusChanged(
    Guid EventId,
    DateTime OccurredOn,
    Guid BankId,
    string OldStatus,
    string NewStatus
);