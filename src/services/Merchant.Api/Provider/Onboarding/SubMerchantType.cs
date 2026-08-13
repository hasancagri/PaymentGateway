namespace Merchant.Api.Provider.Onboarding
{
    // iyzico wire vocab — /onboarding/submerchant subMerchantType değerleri. Domain karşılığı
    // Merchant.Api.Domains.Merchants.MerchantType (023 tip matrisi); çeviri davranış spec'inin işi.
    public enum SubMerchantType
    {
        PERSONAL,
        PRIVATE_COMPANY,
        LIMITED_OR_JOINT_STOCK_COMPANY
    }
}
