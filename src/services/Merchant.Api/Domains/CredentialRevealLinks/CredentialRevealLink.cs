using System.Security.Cryptography;

namespace Merchant.Api.Domains.CredentialRevealLinks;

/// <summary>
/// Tek gösterimlik, süreli credential teslim linki (045). Approve (ya da admin yeniden-teslim)
/// üretir; link <c>{PublicBaseUrl}/onboarding/reveal/{Token}</c> sayfası MerchantId + MerchantKey'i
/// BİR KEZ gösterir — GÖSTERİM tüketir (gösterim = teslim; store 078 ekranından fark budur).
/// Yeniden-teslim aynı merchant'ın yaşayan linklerini öldürür (Kill).
/// </summary>
public class CredentialRevealLink : AggregateRoot
{
    private CredentialRevealLink()
    {
    }

    /// <summary>256-bit rastgele, URL-safe (base64url) erişim token'ı — link = yetki.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Teslim edilecek merchant.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Linkin ölüm anı (üretim + RevealLinkLifetime; Kill geçmişe çeker).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>İlk gösterim anı; dolu ise link tüketilmiştir (tek gösterim).</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>Yeni teslim linki üretir: 256-bit URL-safe token + yaşam süresi.</summary>
    public static ResultDomain<CredentialRevealLink> Create(Guid merchantId, TimeSpan lifetime)
    {
        if (merchantId == Guid.Empty)
            return ResultDomain<CredentialRevealLink>.Error(new MessageItem
            { Property = nameof(MerchantId), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (lifetime <= TimeSpan.Zero)
            return ResultDomain<CredentialRevealLink>.Error(new MessageItem
            { Property = nameof(ExpiresAt), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE });

        return ResultDomain<CredentialRevealLink>.Ok(new CredentialRevealLink
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            MerchantId = merchantId,
            ExpiresAt = DateTimeOffset.UtcNow + lifetime
        });
    }

    /// <summary>Linki tüketir (gösterim anı — gösterim = teslim): süresi geçmiş ya da tüketilmişse RET.</summary>
    public ResultDomain Consume(DateTimeOffset now)
    {
        if (!IsUsable(now))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Token), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

        ConsumedAt = now;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Yaşayan linki öldürür (yeniden-teslim eskiyi geçersiz kılar); tüketilmişte zararsız no-op.</summary>
    public void Kill(DateTimeOffset now)
    {
        if (ConsumedAt is not null)
            return;

        ExpiresAt = now;
        UpdatedTime = DateTime.UtcNow;
    }

    /// <summary>Link hâlâ gösterilebilir mi (süre dolmamış + tüketilmemiş) — saf getter.</summary>
    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && now <= ExpiresAt;
}
