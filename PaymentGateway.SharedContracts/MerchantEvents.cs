namespace PaymentGateway.SharedContracts.MerchantEvents;

public sealed record MerchantCreated(Guid MerchantId, string Name, string Email, string Country, DateTime OccurredOn);
public sealed record MerchantUpdated(Guid MerchantId, string? Name, string? WebhookUrl, DateTime OccurredOn);
public sealed record MerchantStatusChanged(Guid MerchantId, string OldStatus, string NewStatus, DateTime OccurredOn);
public sealed record ApiKeyGenerated(Guid MerchantId, string KeyHash, DateTime OccurredOn);
public sealed record ApiKeyRevoked(Guid MerchantId, string KeyHash, DateTime OccurredOn);