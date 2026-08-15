namespace Payment.Api.Domains.StoredCards;

/// <summary>
/// Kayıtlı kartın kayıt-otoritesi (Payment BC document). Kimlik = opak <see cref="Token"/>. 032
/// (Model A): kart iyzico Saklı Kart'ında durur — gateway PAN SAKLAMAZ, yalnız sağlayıcı kimliklerini
/// (<see cref="CardUserKey"/> + <see cref="CardToken"/>, per-kart) + gösterim alanlarını tutar. Luhn/
/// expiry/AES doğrulaması aggregate'ten kalktı (iyzico doğrular; handler çağırır). Silme soft
/// (<see cref="Revoke"/>).
/// </summary>
public class StoredCard : AggregateRoot
{
    private StoredCard()
    {
    }

    /// <summary>Marten identity; opak, tahmin-edilemez (<c>card_</c> + Guid "N"); immutable; ECommerce'e dönen.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Sahip merchant (tenant sınırı); index; immutable.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>iyzico kullanıcı-kimliği (per-kart, 032/R2 — gruplama ertelendi); ödeme girdisi; immutable.</summary>
    public string CardUserKey { get; private set; } = string.Empty;

    /// <summary>iyzico kart-kimliği; ödeme girdisi; immutable.</summary>
    public string CardToken { get; private set; } = string.Empty;

    /// <summary>İlk 6 hane (iyzico yanıtından; denetim/gösterim); immutable.</summary>
    public string Bin { get; private set; } = string.Empty;

    /// <summary>Son 4 hane (iyzico yanıtından; denetim/gösterim); immutable.</summary>
    public string Last4 { get; private set; } = string.Empty;

    /// <summary>Kart markası (iyzico CardAssociation'dan eşlenir); immutable.</summary>
    public CardBrand Brand { get; private set; }

    /// <summary>Son kullanma (<c>MM/yy</c>); immutable.</summary>
    public string Expiry { get; private set; } = string.Empty;

    /// <summary>Kart sahibi; immutable.</summary>
    public string HolderName { get; private set; } = string.Empty;

    public StoredCardStatus Status { get; private set; }

    /// <summary>
    /// Sağlayıcı (iyzico) kimlikleriyle kayıt oluşturur. PAN/Luhn/expiry doğrulaması BURADA YOK —
    /// iyzico Saklı Kart çağrısı handler'da yapılır, bu fabrika dönen kimlikleri + gösterim alanlarını
    /// sarar. Zorunlu: merchantId, cardUserKey, cardToken. Opak token üretir, <see cref="StoredCardStatus.Active"/> doğar.
    /// </summary>
    /// <remarks>Handler: TokenizeCardCommandHandler</remarks>
    public static ResultDomain<StoredCard> Create(
        Guid merchantId,
        string cardUserKey,
        string cardToken,
        string bin,
        string last4,
        CardBrand brand,
        string expiry,
        string holderName)
    {
        if (merchantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(cardUserKey) ||
            string.IsNullOrWhiteSpace(cardToken))
        {
            return ResultDomain<StoredCard>.Error(new MessageItem
            {
                Property = nameof(cardToken),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        return ResultDomain<StoredCard>.Ok(new StoredCard
        {
            Token = "card_" + Guid.NewGuid().ToString("N"),
            MerchantId = merchantId,
            CardUserKey = cardUserKey.Trim(),
            CardToken = cardToken.Trim(),
            Bin = (bin ?? string.Empty).Trim(),
            Last4 = (last4 ?? string.Empty).Trim(),
            Brand = brand,
            Expiry = (expiry ?? string.Empty).Trim(),
            HolderName = (holderName ?? string.Empty).Trim(),
            Status = StoredCardStatus.Active
        });
    }

    /// <summary>Kartı soft iptal eder (fiziksel durur). Idempotent: zaten Revoked → Ok.</summary>
    /// <remarks>Handler: RevokeCardCommandHandler</remarks>
    public ResultDomain Revoke()
    {
        if (Status == StoredCardStatus.Revoked)
            return ResultDomain.Ok();

        Status = StoredCardStatus.Revoked;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}

/// <summary>
/// Kayıtlı kart yaşam-döngüsü. Silme soft: <see cref="Revoked"/> fiziksel kaydı durdurur
/// (resolve RET). Reactivate yok — yeni kart = yeni tokenize.
/// </summary>
public enum StoredCardStatus
{
    Active = 0,
    Revoked = 1
}

/// <summary>
/// Kart markası (031) — PAN prefix'inden türetilir (<see cref="BrandDetector"/>).
/// BC-içi enum: eski paylaşılan kart taksonomisi (SharedKernel) 021'de silindi; yalnız gösterim/
/// denetim alanı olduğundan cross-BC taksonomi gerekmez.
/// </summary>
public enum CardBrand
{
    Unknown = 0,
    Visa = 1,
    MasterCard = 2,
    Amex = 3,
    Troy = 4
}
