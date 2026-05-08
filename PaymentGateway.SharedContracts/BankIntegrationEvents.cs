namespace PaymentGateway.SharedContracts.BankIntegrationEvents;

// Published by BankIntegration.Api whenever a merchant's routing configuration changes.
// PaymentProcessing consumes this to maintain its local BankRouteSummary read model.
public sealed record BankRouteSynced(
    Guid MerchantId,
    Guid BankId,
    string BankName,
    string Currency,
    decimal BankRate,
    decimal MerchantRate,
    DateTime OccurredOn);