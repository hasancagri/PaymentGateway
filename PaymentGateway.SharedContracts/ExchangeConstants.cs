namespace PaymentGateway.SharedContracts;

public static class ExchangeConstants
{
    public const string BankCommissionSynced = "commission.bank-commission-synced";
    public const string MerchantCommissionSynced = "commission.merchant-commission-synced";
    public const string MerchantBankSynced = "bank-integration.merchant-bank-synced";
    public const string MerchantSynced = "merchant.merchant-synced";
    public const string ApiKeyRevoked = "merchant.api-key-revoked";
    public const string MerchantStatusChanged = "merchant.merchant-status-changed";
}