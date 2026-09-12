namespace Payment.Api.Domains.StoredCards;

/// <summary>
/// 040: Hosted kart-ekleme (iyzico Checkout Form) korelasyon oturumu. Mağazanın conversationId'sini
/// (Id) + CF token'ını + tamamlanınca cardUserKey'i tutar. Aggregate — davranış + invariant içeride.
/// Kart verisi (PAN) burada YOK; yalnız sağlayıcı kimlikleri + durum. Tek-kullanımlık + süre-sınırlı:
/// Completed/Failed tekrar işlenmez.
/// </summary>
public class CardSession : AggregateRoot
{
    private CardSession() { }

    /// <summary>Sahip merchant (tenant sınırı; claim'den çözülür).</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>iyzico Checkout Form token'ı (retrieve için); immutable.</summary>
    public string CheckoutFormToken { get; private set; } = string.Empty;

    /// <summary>Tamamlanınca sağlayıcıdan gelen kullanıcı-kimliği (=pgUserHandle); başlangıçta null.</summary>
    public string? CardUserKey { get; private set; }

    public CardSessionStatus Status { get; private set; }

    /// <summary>Oturum başlangıcı (süre-sınırı hesabı — deterministik test için açık param).</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Yeni Pending oturum. Id = mağaza conversationId'si (iyzico'ya da bu gider).</summary>
    public static CardSession Start(Guid conversationId, Guid merchantId, string checkoutFormToken, DateTimeOffset now)
        => new()
        {
            Id = conversationId,
            MerchantId = merchantId,
            CheckoutFormToken = checkoutFormToken,
            Status = CardSessionStatus.Pending,
            StartedAt = now
        };

    /// <summary>Oturum callback için hâlâ kullanılabilir mi (Pending + süre dolmamış).</summary>
    public bool IsUsable(DateTimeOffset now, TimeSpan ttl) =>
        Status == CardSessionStatus.Pending && now - StartedAt <= ttl;

    /// <summary>Kart başarıyla eklendi — cardUserKey yazılır, oturum tüketilir. Boş cardUserKey reddedilir;
    /// yalnız Pending'den Completed'a geçer (tek sefer).</summary>
    public ResultDomain Complete(string cardUserKey)
    {
        if (string.IsNullOrWhiteSpace(cardUserKey))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(CardUserKey), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        if (Status != CardSessionStatus.Pending)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

        CardUserKey = cardUserKey.Trim();
        Status = CardSessionStatus.Completed;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>İptal/hata → oturum tüketilir, kalıcı kart kaydı yapılmaz. Idempotent (zaten Failed → Ok).</summary>
    public ResultDomain Fail()
    {
        if (Status == CardSessionStatus.Failed)
            return ResultDomain.Ok();

        Status = CardSessionStatus.Failed;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}

/// <summary>Hosted kart-ekleme oturum durumu (R3). Completed/Failed terminal, tekrar işlenmez.</summary>
public enum CardSessionStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}
