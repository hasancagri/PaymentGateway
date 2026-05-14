namespace PaymentGateway.SharedContracts;

public sealed record BankCommissionSynced(
    Guid BankCommissionId,
    Guid BankId,
    CardBrand CardBrand,
    CardType CardType,
    TransactionRegion TransactionRegion,
    decimal Rate,
    DateTime OccurredOn);

public sealed record MerchantCommissionSynced(
    Guid MerchantCommissionId,
    Guid MerchantId,
    Guid BankCommissionId,
    CardBrand CardBrand,
    CardType CardType,
    TransactionRegion TransactionRegion,
    decimal Rate,
    DateTime OccurredOn);

public sealed record MerchantBankSynced(
    Guid MerchantBankId,
    Guid MerchantId,
    Guid BankId,
    string BankName,
    string? IcaMemberId,
    IReadOnlyCollection<string> SupportedCurrencies,
    bool IsActive,
    DateTime OccurredOn);

public sealed record MerchantSynced(
    Guid MerchantId,
    string WebhookUrl,
    bool IsActive,
    DateTime OccurredOn);