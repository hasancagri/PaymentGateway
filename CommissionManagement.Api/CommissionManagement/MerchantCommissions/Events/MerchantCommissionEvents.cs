namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Events;

public sealed record MerchantCommissionDefined(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantCommissionId,
    Guid MerchantId,
    string CardBrand,
    string CardType,
    string TransactionRegion,
    decimal Rate
);

public sealed record MerchantCommissionUpdated(
    Guid EventId,
    DateTime OccurredOn,
    Guid MerchantCommissionId,
    decimal OldRate,
    decimal NewRate
);