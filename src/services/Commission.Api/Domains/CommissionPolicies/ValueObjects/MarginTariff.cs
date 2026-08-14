namespace Commission.Api.Domains.CommissionPolicies.ValueObjects;

/// <summary>
/// Kademe (030) — tarife tablosunun tek satırı: alt sınırdan (dahil) itibaren geçerli oran + sabit
/// ücret. Üst sınırı bir sonraki kademenin alt sınırıdır (hariç); son kademe açık uçludur.
/// Doğrulama tablo bütünlüğü olarak <see cref="MarginTariff.Create"/>'tedir; bu tip saf taşıyıcı.
/// </summary>
public class MarginTier
{
    // Marten/Newtonsoft deserializasyonu için.
    private MarginTier()
    {
    }

    internal MarginTier(decimal fromAmount, decimal ratePercent, decimal fixedFee)
    {
        FromAmount = fromAmount;
        RatePercent = ratePercent;
        FixedFee = fixedFee;
    }

    /// <summary>Kademenin alt sınırı (TL, dahil).</summary>
    public decimal FromAmount { get; private set; }

    /// <summary>Ödenen tutara uygulanan oran (kesir; 0.02 = %2).</summary>
    public decimal RatePercent { get; private set; }

    /// <summary>İşlem başına sabit ücret (TL).</summary>
    public decimal FixedFee { get; private set; }
}

/// <summary>
/// Gateway marj tarifesi (030, FR-001/FR-002) — tutar-kademeli marj tablosu: 0'dan başlayan, kesin
/// artan alt sınırlı kademeler; son kademe açık uçlu. Bracket modeli: işlem tutarının düştüğü TEK
/// kademenin oranı+sabiti TÜM tutara uygulanır (dilimli/birikimli DEĞİL — FR-003). 024'ün tek
/// (oran, sabit) çiftli MarginRule'unun yerini alır; tek kademeli tablo eski davranışla birebir
/// (SC-004). Value object: private ctor + statik <see cref="Create"/>; VO helper serbest (015).
/// </summary>
public class MarginTariff
{
    /// <summary>Tablo üst sınırı — en fazla kademe sayısı.</summary>
    public const int MaxTierCount = 10;

    /// <summary>Kademe başına marj üst sınırı — oran (kesir): %20.</summary>
    public const decimal MaxRatePercent = 0.20m;

    /// <summary>Kademe başına marj üst sınırı — işlem başına sabit ücret: 100 TL.</summary>
    public const decimal MaxFixedFee = 100m;

    private List<MarginTier> _tiers = new();

    // Marten/Newtonsoft deserializasyonu için (NonPublicSetters + AllowNonPublicDefaultConstructor).
    private MarginTariff()
    {
    }

    /// <summary>Kademeler — FromAmount kesin artan sıralı (Create garantiler).</summary>
    public IReadOnlyList<MarginTier> Tiers
    {
        get => _tiers;
        private set => _tiers = value.ToList();
    }

    /// <summary>
    /// Tarife fabrikası (FR-002): en az 1, en çok <see cref="MaxTierCount"/> kademe; ilk kademe
    /// alt sınırı 0; alt sınırlar kesin artan (boşluk/bindirme yapısal imkânsız); her kademede
    /// 0 ≤ oran ≤ <see cref="MaxRatePercent"/> ve 0 ≤ sabit ≤ <see cref="MaxFixedFee"/>.
    /// Hata Property'si sorunlu kademeyi işaret eder (ör. <c>Tiers[2].FromAmount</c>).
    /// </summary>
    public static ResultDomain<MarginTariff> Create(
        IReadOnlyList<(decimal FromAmount, decimal RatePercent, decimal FixedFee)> tiers)
    {
        if (tiers is null || tiers.Count == 0)
            return ResultDomain<MarginTariff>.Error(new MessageItem
            {
                Property = nameof(Tiers),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });

        if (tiers.Count > MaxTierCount)
            return ResultDomain<MarginTariff>.Error(new MessageItem
            {
                Property = nameof(Tiers),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });

        if (tiers[0].FromAmount != 0)
            return ResultDomain<MarginTariff>.Error(new MessageItem
            {
                Property = $"{nameof(Tiers)}[0].{nameof(MarginTier.FromAmount)}",
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });

        for (var i = 0; i < tiers.Count; i++)
        {
            if (i > 0 && tiers[i].FromAmount <= tiers[i - 1].FromAmount)
                return ResultDomain<MarginTariff>.Error(new MessageItem
                {
                    Property = $"{nameof(Tiers)}[{i}].{nameof(MarginTier.FromAmount)}",
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            if (tiers[i].RatePercent < 0 || tiers[i].RatePercent > MaxRatePercent)
                return ResultDomain<MarginTariff>.Error(new MessageItem
                {
                    Property = $"{nameof(Tiers)}[{i}].{nameof(MarginTier.RatePercent)}",
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });

            if (tiers[i].FixedFee < 0 || tiers[i].FixedFee > MaxFixedFee)
                return ResultDomain<MarginTariff>.Error(new MessageItem
                {
                    Property = $"{nameof(Tiers)}[{i}].{nameof(MarginTier.FixedFee)}",
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
                });
        }

        return ResultDomain<MarginTariff>.Ok(new MarginTariff
        {
            _tiers = tiers.Select(t => new MarginTier(t.FromAmount, t.RatePercent, t.FixedFee)).ToList()
        });
    }

    /// <summary>
    /// Kademe seçimi (FR-003): <c>FromAmount &lt;= paidPrice</c> olan SON kademe — tam sınır değeri
    /// üst kademeye düşer; ilk kademe 0'dan başladığı için pozitif her tutar bir kademe bulur.
    /// </summary>
    public MarginTier ResolveTier(decimal paidPrice)
    {
        var selected = _tiers[0];
        foreach (var tier in _tiers)
        {
            if (tier.FromAmount <= paidPrice)
                selected = tier;
            else
                break;
        }

        return selected;
    }
}
