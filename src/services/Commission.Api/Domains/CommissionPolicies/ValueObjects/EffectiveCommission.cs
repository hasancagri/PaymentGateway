namespace Commission.Api.Domains.CommissionPolicies.ValueObjects;

/// <summary>
/// Bir işlem için efektif komisyon dökümü (024, FR-006/FR-007) — hesap sonucu, kalıcı değil.
/// Efektif komisyon = iyzico maliyeti + gateway marjı; net hakediş = ödenen tutar − efektif komisyon.
/// <see cref="CommissionPolicy.CalculateEffectiveCommission"/> döndürür. Value object: private ctor +
/// statik <see cref="Create"/>.
/// </summary>
public class EffectiveCommission
{
    private EffectiveCommission(
        decimal paidPrice,
        int installment,
        decimal iyzicoCost,
        decimal gatewayMargin,
        decimal totalEffectiveCommission,
        decimal netPayout)
    {
        PaidPrice = paidPrice;
        Installment = installment;
        IyzicoCost = iyzicoCost;
        GatewayMargin = gatewayMargin;
        TotalEffectiveCommission = totalEffectiveCommission;
        NetPayout = netPayout;
    }

    /// <summary>İşlemin ödenen tutarı (girdi).</summary>
    public decimal PaidPrice { get; }

    /// <summary>Taksit sayısı (girdi, bilgi amaçlı).</summary>
    public int Installment { get; }

    /// <summary>iyzico maliyeti = IyzicoCommission + IyzicoFee (ayrıştırılmış toplam).</summary>
    public decimal IyzicoCost { get; }

    /// <summary>Gateway marjı = PaidPrice·RatePercent + FixedFee (2 ondalık yuvarlı).</summary>
    public decimal GatewayMargin { get; }

    /// <summary>Toplam efektif komisyon = IyzicoCost + GatewayMargin.</summary>
    public decimal TotalEffectiveCommission { get; }

    /// <summary>Merchant net hakediş = PaidPrice − TotalEffectiveCommission.</summary>
    public decimal NetPayout { get; }

    /// <summary>Hesap-sonucu fabrikası (aritmetiği çağıran aggregate metodu yapar).</summary>
    public static EffectiveCommission Create(
        decimal paidPrice,
        int installment,
        decimal iyzicoCost,
        decimal gatewayMargin,
        decimal totalEffectiveCommission,
        decimal netPayout) =>
        new(paidPrice, installment, iyzicoCost, gatewayMargin, totalEffectiveCommission, netPayout);
}
