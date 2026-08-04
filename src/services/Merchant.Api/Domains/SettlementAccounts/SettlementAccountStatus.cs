namespace Merchant.Api.Domains.SettlementAccounts;

/// <summary>
/// Settlement hesabının yaşam döngüsü durumu. Düz enum (mevcut <c>MerchantStatus</c> konvansiyonu).
/// Settlement için aktif/pasif yeterli — Suspended yok (YAGNI).
/// </summary>
public enum SettlementAccountStatus
{
    Active = 1,
    Passive = 2
}