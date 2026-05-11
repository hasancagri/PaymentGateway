namespace PaymentGateway.SharedContracts.CommissionEvents;

public sealed record BankCommissionSynced(
    Guid BankCommissionId,
    Guid BankId,
    string CardBrand,
    string CardType,
    string TransactionRegion,
    decimal Rate,
    DateTime OccurredOn);

public sealed record BankCommissionRateUpdated(
    Guid BankCommissionId,
    decimal NewRate,
    DateTime OccurredOn);

public sealed record MerchantCommissionSynced(
    Guid MerchantCommissionId,
    Guid MerchantId,
    Guid BankCommissionId,
    decimal Rate,
    DateTime OccurredOn);

public sealed record MerchantCommissionRateUpdated(
    Guid MerchantCommissionId,
    decimal NewRate,
    DateTime OccurredOn);