using System.Security.Cryptography;

namespace Merchant.Api.Domains.Merchants;

/// <summary>
/// Tek kullanımlık, süreli hassas-veri hosted ekran oturumu (048). Agent tool'u üretir, link
/// <c>{PublicBaseUrl}/merchants/sensitive/{Token}</c> ile taşınır; başarılı POST <c>Consume</c> ile
/// oturumu öldürür. GET tüketmez (form açıp vazgeçmek linki öldürmez, süre öldürür). Yeni link
/// eskisini iptal etmez — süre öldürür (bilinçli basitlik, data-model.md). ECom CredentialEntrySession
/// ikizi + <c>MerchantId</c> (görüntüleme hedefi).
/// </summary>
public class SensitiveEntrySession : AggregateRoot
{
    private SensitiveEntrySession()
    {
    }

    /// <summary>256-bit rastgele, URL-safe (base64url) yetki token'ı — link = yetki (İLKE V capability-link istisnası).</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Link hangi merchant için (görüntüleme/düzenleme hedefi).</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Linki üreten admin (denetim izi için; token izde YER ALMAZ).</summary>
    public Guid RequestedByUserId { get; private set; }

    /// <summary>Oturumun ölüm anı (üretim + LinkLifetime); geçmişse oturum pasif ölüdür, ayrı işaret yok.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Başarılı POST anı; dolu ise oturum tüketilmiştir (tek kullanım).</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>Yeni ekran oturumu üretir: 256-bit URL-safe token + yaşam süresi. Boş merchant/kullanıcı ya da pozitif olmayan süre RET.</summary>
    public static ResultDomain<SensitiveEntrySession> Create(Guid merchantId, Guid requestedByUserId, TimeSpan lifetime)
    {
        var messages = new List<MessageItem>();

        if (merchantId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(MerchantId), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        if (requestedByUserId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(RequestedByUserId), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        if (lifetime <= TimeSpan.Zero)
            messages.Add(new MessageItem { Property = nameof(ExpiresAt), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE });

        if (messages.Count > 0)
            return ResultDomain<SensitiveEntrySession>.Error(messages);

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return ResultDomain<SensitiveEntrySession>.Ok(new SensitiveEntrySession
        {
            Token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            MerchantId = merchantId,
            RequestedByUserId = requestedByUserId,
            ExpiresAt = DateTimeOffset.UtcNow + lifetime
        });
    }

    /// <summary>Oturumu tüketir (tek kullanım invariant'ı): süresi geçmiş ya da zaten tüketilmişse RET, değilse işaretler.</summary>
    public ResultDomain Consume(DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Token),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });
        }

        ConsumedAt = now;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Oturum hâlâ kullanılabilir mi (süre dolmamış + tüketilmemiş) — saf getter.</summary>
    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && now <= ExpiresAt;
}
