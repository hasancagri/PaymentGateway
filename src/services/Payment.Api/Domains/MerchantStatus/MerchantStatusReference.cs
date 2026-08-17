namespace Payment.Api.Domains.MerchantStatus;

/// <summary>
/// 038: merchant.lifecycle fanout'undan beslenen event-fed statü referansı (010 Reference deseni).
/// Aggregate DEĞİL — davranış taşımaz; yazan tek yer <see cref="MerchantLifecycleEventHandler"/>,
/// okuyan tek yer ChargeSavedCardForAgent (çekim statü kapısı: kayıt yok veya Status != "Active" →
/// fail-closed RET). Status event'teki string aynen tutulur — Merchant BC enum'u sızmaz (012 kuralı).
/// </summary>
public class MerchantStatusReference
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}