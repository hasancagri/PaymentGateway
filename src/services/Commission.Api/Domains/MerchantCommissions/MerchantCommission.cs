using Commission.Api.Infrastructure;

namespace Commission.Api.Domains.MerchantCommissions;

/// <summary>
/// Merchant'ın belirli bir kombinasyon için gateway'e ödediği komisyon oranı. Banka-bağımsız:
/// tek bir BankCommission'a bağlanmaz, kombinasyona <c>(MerchantId, Criteria)</c> bağlanır.
/// <c>Rate &gt; 0</c> tek invariant'tır (sanity). Banka oranıyla karşılaştırma aggregate'te YOK —
/// tavan-altı işareti okuma anında (GetMerchantCommissions) türetilir, saklanmaz.
/// Tenant YOK (düz MerchantId filtresi). MerchantId Merchant.Api'ye çağrı YAPILMADAN Guid olarak tutulur.
/// </summary>
public class MerchantCommission : AggregateRoot
{
    private MerchantCommission()
    {
    }

    /// <summary>Opak merchant referansı (Merchant.Api'ye çağrı yok).</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Kart markası × tip × bölge × taksit. Benzersizlik: (MerchantId, Criteria).</summary>
    public Criteria Criteria { get; private set; } = null!;

    /// <summary>Yüzde oran; invariant: kesin pozitif (> 0).</summary>
    public decimal Rate { get; private set; }

    /// <summary>Kart taksonomi şema sürümü (bkz. BankCommission.TaxonomyVersion). Migration işareti.</summary>
    public int TaxonomyVersion { get; private set; }

    public static ResultDomain<MerchantCommission> Create(Guid merchantId, Criteria criteria, decimal rate)
    {
        if (merchantId == Guid.Empty)
        {
            return ResultDomain<MerchantCommission>.Error(new MessageItem
            {
                Property = nameof(MerchantId),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (criteria is null)
        {
            return ResultDomain<MerchantCommission>.Error(new MessageItem
            {
                Property = nameof(Criteria),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (rate <= 0)
        {
            return ResultDomain<MerchantCommission>.Error(new MessageItem
            {
                Property = nameof(Rate),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        return ResultDomain<MerchantCommission>.Ok(new MerchantCommission
        {
            MerchantId = merchantId,
            Criteria = criteria,
            Rate = rate,
            TaxonomyVersion = CardTaxonomyRemap.CurrentVersion // yeni kayıt zaten kanonik
        });
    }

    /// <summary>Migration: remap edilmiş kanonik Criteria'yı uygular + şema sürümünü günceller.</summary>
    public void MigrateTaxonomy(Criteria remapped)
    {
        Criteria = remapped;
        TaxonomyVersion = CardTaxonomyRemap.CurrentVersion;
        UpdatedTime = DateTime.UtcNow;
    }

    public ResultDomain UpdateRate(decimal rate)
    {
        if (rate <= 0)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Rate),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        Rate = rate;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}