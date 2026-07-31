namespace Merchant.Api.Domains.Merchants;

/// <summary>
/// Merchant yaşam döngüsü durumu. Şimdilik düz enum (kullanıcı direktifi); ileride gerekirse
/// Enumeration smart-enum'a dönüştürülür. Referans mimari de status için düz enum kullanıyor.
/// </summary>
public enum MerchantStatus
{
    Active = 1,
    Passive = 2,
    Suspended = 3
}