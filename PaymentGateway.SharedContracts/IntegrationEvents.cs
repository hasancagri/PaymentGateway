namespace PaymentGateway.SharedContracts;

public sealed record BankCommissionUpdated(
    Guid BankCommissionId,
    Guid BankId,
    CardBrand CardBrand,
    CardType CardType,
    TransactionRegion TransactionRegion,
    decimal Rate,
    DateTime OccurredOn);

public sealed record MerchantCommissionUpdated(
    Guid MerchantCommissionId,
    Guid MerchantId,
    Guid BankCommissionId,
    CardBrand CardBrand,
    CardType CardType,
    TransactionRegion TransactionRegion,
    decimal Rate,
    DateTime OccurredOn);