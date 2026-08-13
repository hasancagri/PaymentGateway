namespace Commission.Api.Domains.CommissionPolicies.ValueObjects;

/// <summary>
/// Gateway marjı (024, FR-013) — iyzico maliyetinin üstüne konan kâr eklentisi: bir oran (%) +
/// işlem başına sabit ücret. iyzico'nun kendi komisyon yapısını (MerchantCommissionRate oran +
/// IyziCommissionFee sabit) aynalar. Taksit tier eşlemesi YOK (iyzico maliyeti zaten taksite göre
/// raporda değişir). Value object: private ctor + statik <see cref="Create"/>; VO helper serbest (015).
/// </summary>
public class MarginRule
{
    /// <summary>Marj üst sınırı — oran (kesir): %20.</summary>
    public const decimal MaxRatePercent = 0.20m;

    /// <summary>Marj üst sınırı — işlem başına sabit ücret: 100 TL.</summary>
    public const decimal MaxFixedFee = 100m;

    // Marten/Newtonsoft deserializasyonu için (NonPublicSetters + AllowNonPublicDefaultConstructor).
    private MarginRule()
    {
    }

    private MarginRule(decimal ratePercent, decimal fixedFee)
    {
        RatePercent = ratePercent;
        FixedFee = fixedFee;
    }

    /// <summary>Ödenen tutara uygulanan oran (kesir; 0.015 = %1.5). 0 ≤ oran ≤ <see cref="MaxRatePercent"/>.</summary>
    public decimal RatePercent { get; private set; }

    /// <summary>İşlem başına sabit ücret (TL). 0 ≤ ücret ≤ <see cref="MaxFixedFee"/>.</summary>
    public decimal FixedFee { get; private set; }

    /// <summary>
    /// Marj fabrikası: negatif oran/ücret VE tanımlı üst sınır aşımı reddedilir (FR-004).
    /// Geçerse VO döner.
    /// </summary>
    public static ResultDomain<MarginRule> Create(decimal ratePercent, decimal fixedFee)
    {
        if (ratePercent < 0 || ratePercent > MaxRatePercent)
            return ResultDomain<MarginRule>.Error(new MessageItem
            {
                Property = nameof(RatePercent),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });

        if (fixedFee < 0 || fixedFee > MaxFixedFee)
            return ResultDomain<MarginRule>.Error(new MessageItem
            {
                Property = nameof(FixedFee),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });

        return ResultDomain<MarginRule>.Ok(new MarginRule(ratePercent, fixedFee));
    }
}
