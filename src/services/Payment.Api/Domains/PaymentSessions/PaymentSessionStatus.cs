namespace Payment.Api.Domains.PaymentSessions;

/// <summary>
/// Ödeme oturumu faz makinesi. A2A task yaşam döngüsüne izdüşer
/// (Opened→submitted/working, QuoteProvided→input-required, InstallmentSelected→completed, Failed→failed).
/// 007 dışı fazlar (Awaiting3D / Completed) çekim feature'ında eklenecek.
/// </summary>
public enum PaymentSessionStatus
{
    /// <summary>Create sonrası, henüz taksit sunulmadı (geçici ara durum).</summary>
    Opened = 0,

    /// <summary>Faz 1 tamam: taksit listesi sunuldu (A2A input-required).</summary>
    QuoteProvided = 1,

    /// <summary>Faz 2: kullanıcı taksit seçti — 007'nin terminal fazı (çekim seam'e devredilir).</summary>
    InstallmentSelected = 2,

    /// <summary>Geçersiz token / POS yok / sepet ≤ 0 / boş taksit listesi.</summary>
    Failed = 3
}