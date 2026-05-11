namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Events;

public sealed record MerchantCreated(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    string MerchantName,
    string Email,
    string Country
);

public sealed record MerchantUpdated(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId
);

public sealed record MerchantStatusChanged(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    string OldStatus,
    string NewStatus,
    string Reason
);

public sealed record ApiKeyGenerated(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    Guid ApiKeyId
);

public sealed record ApiKeyRevoked(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    Guid ApiKeyId
);

public sealed record BankAccountAdded(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    Guid BankAccountId,
    string Iban,
    string Currency
);

public sealed record BankAccountRemoved(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    Guid BankAccountId
);

public sealed record CurrencyAdded(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    string CurrencyCode
);

public sealed record CurrencyRemoved(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantId,
    string CurrencyCode
);