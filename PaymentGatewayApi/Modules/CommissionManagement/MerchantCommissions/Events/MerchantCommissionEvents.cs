namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Events;

public sealed record MerchantCommissionDefined(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantCommissionId,
    Guid MerchantId,
    string CardBrand,
    string CardType,
    string TransactionRegion,
    decimal Rate
) : IDomainEvent;

public sealed record MerchantCommissionUpdated(
    Guid EventId,
    DateTime OccuredOn,
    Guid MerchantCommissionId,
    decimal OldRate,
    decimal NewRate
) : IDomainEvent;