using System.Security.Cryptography;

namespace Merchant.Api.Domains.OnboardingFormSessions;

/// <summary>
/// Store-tetikli, süreli + tek başvuruluk hosted form oturumu (045). Store S2S ile açtırır, link
/// <c>{PublicBaseUrl}/onboarding/form/{Token}</c> başvuru formunu taşır. Başarılı başvuru oturumu
/// tüketir; doğrulama hatası TÜKETMEZ (düzeltip yeniden gönderilebilir). Süresi dolan oturum pasif
/// ölüdür; store yeni oturum ister.
/// </summary>
public class OnboardingFormSession : AggregateRoot
{
    private OnboardingFormSession()
    {
    }

    /// <summary>256-bit rastgele, URL-safe (base64url) erişim token'ı — link = yetki.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Başvuru kimliği e-postası (normalize, küçük harf) — store'un verdiği.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Oturumun ölüm anı (üretim + FormLinkLifetime).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Başarılı başvuru anı; dolu ise oturum tüketilmiştir (tek başvuruluk).</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>Yeni form oturumu üretir: 256-bit URL-safe token + yaşam süresi; e-posta normalize edilir.</summary>
    public static ResultDomain<OnboardingFormSession> Create(string email, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(email))
            return ResultDomain<OnboardingFormSession>.Error(new MessageItem
            { Property = nameof(Email), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (lifetime <= TimeSpan.Zero)
            return ResultDomain<OnboardingFormSession>.Error(new MessageItem
            { Property = nameof(ExpiresAt), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE });

        return ResultDomain<OnboardingFormSession>.Ok(new OnboardingFormSession
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            Email = email.Trim().ToLowerInvariant(),
            ExpiresAt = DateTimeOffset.UtcNow + lifetime
        });
    }

    /// <summary>Oturumu tüketir (başarılı başvuru anı): süresi geçmiş ya da zaten tüketilmişse RET.</summary>
    public ResultDomain Consume(DateTimeOffset now)
    {
        if (!IsUsable(now))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Token), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

        ConsumedAt = now;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Oturum hâlâ kullanılabilir mi (süre dolmamış + tüketilmemiş) — saf getter.</summary>
    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && now <= ExpiresAt;
}
