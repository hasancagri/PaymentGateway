namespace Merchant.Api.Domains.RegisterRequests;

/// <summary>
/// Domain-control challenge doğrulama sonucu. Talep yalnız <see cref="Passed"/> ile
/// <see cref="RegisterRequestStatus.Pending"/>'e ilerler. 015: challenge ayrı aggregate değil,
/// <see cref="RegisterRequest"/>'in alanı olduğundan bu enum da RegisterRequests altında yaşar.
/// </summary>
public enum ChallengeOutcome
{
    /// <summary>Henüz doğrulanmadı (bilet üretildi, aday değeri yayınlamadı).</summary>
    Pending = 1,

    /// <summary>Doğrulandı — aday beklenen değeri doğru yayınladı (sahiplik ispatı).</summary>
    Passed = 2,

    /// <summary>Değer yok/yanlış — aday yayınlayıp tekrar deneyebilir (bilet geçerli kalır).</summary>
    Failed = 3,

    /// <summary>Bilet süresi doldu — yeni bilet üretilip tekrar denenmeli.</summary>
    Expired = 4
}