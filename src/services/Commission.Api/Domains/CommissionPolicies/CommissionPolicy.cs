using System.Globalization;
using Commission.Api.Domains.CommissionPolicies.ValueObjects;

namespace Commission.Api.Domains.CommissionPolicies;

/// <summary>
/// Gateway marj politikası (024) — bir merchant için iyzico maliyetinin üstüne uygulanacak gateway
/// marjını (oran + sabit ücret) tutar ve verili işlem bağlamı için efektif komisyonu hesaplar.
/// İş kuralları (marj doğrulama, statü geçişi, hesap aritmetiği + tutarsızlık reddi) burada yaşar;
/// iyzico maliyeti hesaba GİRDİ olarak gelir (işlem-sonrası rapordan; FR-012) — canlı iyzico çağrısı
/// YOK. Tekil-aktif kuralı (FR-005) handler-sorgusuyla uygulanır (aggregate cross-aggregate görmez).
/// </summary>
public class CommissionPolicy : AggregateRoot
{
    // Marten/Newtonsoft deserializasyonu için.
    private CommissionPolicy()
    {
    }

    /// <summary>Politikanın bağlı olduğu merchant (dış referans — Merchant BC; cross-BC doğrulanmaz).</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Gateway marjı — oran (%) + sabit ücret.</summary>
    public MarginRule Margin { get; private set; } = default!;

    public CommissionPolicyStatus Status { get; private set; } = CommissionPolicyStatus.Active;

    /// <summary>
    /// Politika fabrikası: boş merchant reddi + marj doğrulama (<see cref="MarginRule.Create"/>).
    /// Geçerse Active doğar.
    /// </summary>
    /// <remarks>Handler: CreateCommissionPolicyCommandHandler</remarks>
    public static ResultDomain<CommissionPolicy> Create(Guid merchantId, decimal ratePercent, decimal fixedFee)
    {
        if (merchantId == Guid.Empty)
            return ResultDomain<CommissionPolicy>.Error(new MessageItem
            {
                Property = nameof(MerchantId),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });

        var margin = MarginRule.Create(ratePercent, fixedFee);
        if (!margin.IsSuccess)
            return ResultDomain<CommissionPolicy>.Error(margin.Messages);

        return ResultDomain<CommissionPolicy>.Ok(new CommissionPolicy
        {
            MerchantId = merchantId,
            Margin = margin.Data!,
            Status = CommissionPolicyStatus.Active
        });
    }

    /// <summary>
    /// Marjı günceller (FR-002) — yeni oran/ücret doğrulanır, geçerse yürürlüğe girer. İleriye
    /// dönük (geçmiş hesaplar yeniden fiyatlanmaz).
    /// </summary>
    /// <remarks>Handler: UpdateCommissionPolicyMarginCommandHandler</remarks>
    public ResultDomain UpdateMargin(decimal ratePercent, decimal fixedFee)
    {
        var margin = MarginRule.Create(ratePercent, fixedFee);
        if (!margin.IsSuccess)
            return ResultDomain.Error(margin.Messages);

        Margin = margin.Data!;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Statü geçişi (FR-003): Active/Passive arası. Aynı statüye geçiş idempotent no-op —
    /// <c>Ok(false)</c> (değişmedi), gerçek değişiklik <c>Ok(true)</c>.
    /// </summary>
    /// <remarks>Handler: ChangeCommissionPolicyStatusCommandHandler</remarks>
    public ResultDomain<bool> ChangeStatus(CommissionPolicyStatus newStatus)
    {
        if (Status == newStatus)
            return ResultDomain<bool>.Ok(false);

        Status = newStatus;
        IsActive = newStatus == CommissionPolicyStatus.Active;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain<bool>.Ok(true);
    }

    /// <summary>
    /// Efektif komisyon + net hakediş hesabı (FR-006/FR-007). iyzico maliyeti string girdi olarak
    /// gelir (TransactionReportItem alanları — FR-012), InvariantCulture ile ayrıştırılır; eksik/
    /// ayrıştırılamaz değer reddedilir (sessiz 0 YOK). Pasif politikada hesaplama yapılmaz (FR-008/003);
    /// efektif komisyon ödenen tutarı aşarsa reddedilir (negatif hakediş üretilmez — FR-009).
    /// Yuvarlama: 2 ondalık (kuruş), AwayFromZero (deterministik — SC-002).
    /// </summary>
    /// <remarks>Handler: CalculateEffectiveCommissionQueryHandler</remarks>
    public ResultDomain<EffectiveCommission> CalculateEffectiveCommission(
        decimal paidPrice,
        string iyzicoCommission,
        string iyzicoFee,
        int installment)
    {
        if (Status != CommissionPolicyStatus.Active)
            return ResultDomain<EffectiveCommission>.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INACTIVE_VALUE_ERROR
            });

        if (paidPrice <= 0)
            return ResultDomain<EffectiveCommission>.Error(new MessageItem
            {
                Property = nameof(paidPrice),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });

        if (!decimal.TryParse(iyzicoCommission, NumberStyles.Number, CultureInfo.InvariantCulture, out var commissionCost))
            return ResultDomain<EffectiveCommission>.Error(new MessageItem
            {
                Property = nameof(iyzicoCommission),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });

        if (!decimal.TryParse(iyzicoFee, NumberStyles.Number, CultureInfo.InvariantCulture, out var feeCost))
            return ResultDomain<EffectiveCommission>.Error(new MessageItem
            {
                Property = nameof(iyzicoFee),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });

        if (commissionCost < 0 || feeCost < 0)
            return ResultDomain<EffectiveCommission>.Error(new MessageItem
            {
                Property = nameof(iyzicoCommission),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE
            });

        var iyzicoCost = commissionCost + feeCost;
        var gatewayMargin = Math.Round(
            paidPrice * Margin.RatePercent + Margin.FixedFee, 2, MidpointRounding.AwayFromZero);
        var effective = iyzicoCost + gatewayMargin;

        if (effective > paidPrice)
            return ResultDomain<EffectiveCommission>.Error(new MessageItem
            {
                Property = nameof(effective),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });

        var netPayout = paidPrice - effective;

        return ResultDomain<EffectiveCommission>.Ok(EffectiveCommission.Create(
            paidPrice, installment, iyzicoCost, gatewayMargin, effective, netPayout));
    }
}
