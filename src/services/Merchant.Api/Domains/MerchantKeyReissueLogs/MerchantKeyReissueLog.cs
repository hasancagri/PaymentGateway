namespace Merchant.Api.Domains.MerchantKeyReissueLogs;

/// <summary>
/// 046 FR-007: salt-append key yenileme denetim kaydı. Aggregate DEĞİL (davranış/invariant yok —
/// İLKE II log istisnası); her başarılı reissue bir satır üretir. Yalnız <c>session.Store</c> (insert);
/// güncelleme/silme yolu YOK — "kim, ne zaman, neden" izi değiştirilemez. Yazan tek yer reissue
/// handler'ı; okuyan geçmiş sorgusu (merchantId ile liste).
/// </summary>
public class MerchantKeyReissueLog
{
    /// <summary>Kayıt kimliği (her yenileme ayrı satır).</summary>
    public Guid Id { get; set; }

    /// <summary>Key'i yenilenen merchant.</summary>
    public Guid MerchantId { get; set; }

    /// <summary>Yenileme anı.</summary>
    public DateTimeOffset ReissuedAt { get; set; }

    /// <summary>Tetikleyen aktör (m2m/store kimliği).</summary>
    public string InitiatedBy { get; set; } = string.Empty;

    /// <summary>Opsiyonel neden notu (davranışı değiştirmez).</summary>
    public string? Reason { get; set; }
}