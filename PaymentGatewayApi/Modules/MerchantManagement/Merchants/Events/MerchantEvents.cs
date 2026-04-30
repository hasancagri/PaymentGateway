namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Events;

public sealed record MerchantCreated(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantId,
    string MerchantName,
    string Email,
    string Country
) : IDomainEvent;

public sealed record MerchantUpdated(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantId
) : IDomainEvent;

public sealed record MerchantStatusChanged(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantId,
    string OldStatus,
    string NewStatus,
    string Reason
) : IDomainEvent;

public sealed record ApiKeyGenerated(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantId,
    Guid ApiKeyId
) : IDomainEvent;

public sealed record ApiKeyRevoked(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantId,
    Guid ApiKeyId
) : IDomainEvent;

public sealed record BankAccountAdded(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantId,
    Guid BankAccountId,
    string Iban,
    string Currency
) : IDomainEvent;

public sealed record BankAccountRemoved(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantId,
    Guid BankAccountId
) : IDomainEvent;

public sealed record CurrencyAdded(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantId,
    string CurrencyCode
) : IDomainEvent;

public sealed record CurrencyRemoved(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantId,
    string CurrencyCode
) : IDomainEvent;