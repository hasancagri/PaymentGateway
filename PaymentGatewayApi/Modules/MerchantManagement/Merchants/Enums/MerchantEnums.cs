namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Enums;

public enum MerchantStatus
{
    Active    = 1,
    Passive   = 2,
    Suspended = 3
}

public enum ApiKeyStatus
{
    Active  = 1,
    Revoked = 2,
    Expired = 3
}

public enum BankAccountType
{
    Primary   = 1,
    Secondary = 2
}
