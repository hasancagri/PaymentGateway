namespace Merchant.Api.Domains.Merchants;

/// <summary>
/// Merchant yaşam döngüsü durumu (023). Yeni merchant Active doğar (onboarding/Provisioning
/// zinciri söküldü — ileride ayrı spec). Token verme statü-kapılı: yalnız Active
/// (Identity.Server tüketicisi karar verir, string taşınır — BC enum'u Shared'a sızmaz).
/// </summary>
public enum MerchantStatus
{
    Active = 1,
    Passive = 2,
    Suspended = 3
}
