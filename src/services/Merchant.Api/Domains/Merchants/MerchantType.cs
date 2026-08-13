namespace Merchant.Api.Domains.Merchants;

/// <summary>
/// İşyeri tipi (023) — hangi yasal alanların zorunlu olduğunu belirler (tip-uyum matrisi,
/// bkz. <see cref="Merchant"/>). İyzico <c>SubMerchantType</c> string sabitleriyle eşleme
/// iyzico kayıt entegrasyonunda (ayrı iş) yapılır; sağlayıcı tipi domain'e girmez.
/// </summary>
public enum MerchantType
{
    /// <summary>Şahıs — IdentityNumber zorunlu.</summary>
    Personal = 1,

    /// <summary>Şahıs şirketi — IdentityNumber + TaxOffice + LegalCompanyTitle zorunlu.</summary>
    PrivateCompany = 2,

    /// <summary>Sermaye şirketi — TaxOffice + TaxNumber + LegalCompanyTitle zorunlu.</summary>
    LimitedOrJointStockCompany = 3
}
