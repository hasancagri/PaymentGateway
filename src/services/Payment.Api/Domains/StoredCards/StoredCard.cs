using System.Globalization;
using Payment.Api.CardVault;

namespace Payment.Api.Domains.StoredCards;

/// <summary>
/// Kayıtlı kartın kayıt-otoritesi (Payment BC document). Kimlik = opak <see cref="Token"/>. Ham PAN
/// yalnız korunmuş <see cref="EncryptedPan"/> olarak durur (write-only bu feature'da); resolve
/// yalnız <see cref="Bin"/> kullanır → PAN Payment BC sınırını ham geçmez. bin/last4/brand tokenize
/// anında PAN'dan türetilir ve immutable; expiry/holder <see cref="UpdateDetails"/> ile değişir;
/// silme soft (<see cref="Revoke"/>).
/// </summary>
public class StoredCard : AggregateRoot
{
    private StoredCard()
    {
    }

    /// <summary>Marten identity; opak, tahmin-edilemez (<c>card_</c> + Guid "N"); PAN'dan türetilmez; immutable.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Sahip merchant (tenant sınırı); index; immutable.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary><see cref="IPanProtector"/> ile korunmuş PAN (enc-at-rest); hiç okunmaz/dönmez; immutable.</summary>
    public string EncryptedPan { get; private set; } = string.Empty;

    /// <summary>PAN'dan türetilen ilk 6 hane; resolve girdisi; immutable.</summary>
    public string Bin { get; private set; } = string.Empty;

    /// <summary>PAN son 4 hane; denetim/gösterim; immutable.</summary>
    public string Last4 { get; private set; } = string.Empty;

    /// <summary>PAN prefix'inden türetilen marka; immutable.</summary>
    public CardBrand Brand { get; private set; }

    /// <summary>Son kullanma (<c>MM/yy</c>); <see cref="UpdateDetails"/> ile değişebilir.</summary>
    public string Expiry { get; private set; } = string.Empty;

    /// <summary>Kart sahibi; <see cref="UpdateDetails"/> ile değişebilir.</summary>
    public string HolderName { get; private set; } = string.Empty;

    public StoredCardStatus Status { get; private set; }

    /// <summary>
    /// PAN'ı doğrular (Luhn) + son kullanmayı (gelecekte) kontrol eder, opak token üretir,
    /// bin/last4/brand'i türetir, PAN'ı korur ve <see cref="StoredCardStatus.Active"/> kart döndürür.
    /// PAN/expiry/holder boş olamaz. Non-idempotent: aynı PAN her çağrıda YENİ token (FR-014).
    /// </summary>
    /// <remarks>Handler: TokenizeCardCommandHandler</remarks>
    public static ResultDomain<StoredCard> Create(
        Guid merchantId, string pan, string expiry, string holderName, IPanProtector protector)
    {
        if (merchantId == Guid.Empty || string.IsNullOrWhiteSpace(pan) ||
            string.IsNullOrWhiteSpace(expiry) || string.IsNullOrWhiteSpace(holderName))
        {
            return ResultDomain<StoredCard>.Error(new MessageItem
            {
                Property = nameof(pan),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (!LuhnValidator.IsValid(pan))
        {
            return ResultDomain<StoredCard>.Error(new MessageItem
            {
                Property = nameof(pan),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
            });
        }

        if (!DateTime.TryParseExact(expiry.Trim(), "MM/yy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed) ||
            new DateTime(parsed.Year, parsed.Month, 1).AddMonths(1).AddDays(-1) < DateTime.UtcNow.Date)
        {
            return ResultDomain<StoredCard>.Error(new MessageItem
            {
                Property = nameof(expiry),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
            });
        }

        var digits = pan.Trim();
        return ResultDomain<StoredCard>.Ok(new StoredCard
        {
            Token = "card_" + Guid.NewGuid().ToString("N"),
            MerchantId = merchantId,
            EncryptedPan = protector.Protect(digits),
            Bin = BinExtractor.Extract(digits),
            Last4 = Last4Extractor.Extract(digits),
            Brand = BrandDetector.Detect(digits),
            Expiry = expiry.Trim(),
            HolderName = holderName.Trim(),
            Status = StoredCardStatus.Active,
        });
    }

    /// <summary>
    /// Son kullanma + kart sahibini günceller (PAN/token/bin/last4/brand DOKUNULMAZ). Yalnız
    /// <see cref="StoredCardStatus.Active"/> kartta; expiry gelecekte olmalı.
    /// </summary>
    /// <remarks>Handler: UpdateCardCommandHandler</remarks>
    public ResultDomain UpdateDetails(string expiry, string holderName)
    {
        if (Status != StoredCardStatus.Active)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INACTIVE_VALUE_ERROR
            });
        }

        if (string.IsNullOrWhiteSpace(expiry) || string.IsNullOrWhiteSpace(holderName))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(holderName),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (!DateTime.TryParseExact(expiry.Trim(), "MM/yy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed) ||
            new DateTime(parsed.Year, parsed.Month, 1).AddMonths(1).AddDays(-1) < DateTime.UtcNow.Date)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(expiry),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
            });
        }

        Expiry = expiry.Trim();
        HolderName = holderName.Trim();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Kartı soft iptal eder (fiziksel durur; resolve/update RET). Idempotent: zaten Revoked → Ok.</summary>
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
