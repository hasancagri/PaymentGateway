namespace Merchant.Api.Domains.RegisterRequests;

/// <summary>
/// Kayıt başvurusu durumu (029) — Pending'den iki terminal duruma akar; tarihçe silinmez.
/// </summary>
public enum RegisterRequestStatus
{
    /// <summary>Admin kararı bekliyor.</summary>
    Pending = 1,

    /// <summary>Onaylandı — merchant doğdu, MerchantId bağlı (terminal).</summary>
    Approved = 2,

    /// <summary>Reddedildi — RejectReason dolu; aynı e-posta yeniden başvurabilir (terminal).</summary>
    Rejected = 3
}
