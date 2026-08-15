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
        decimal providerCost,
        decimal gatewayMargin,
        decimal totalEffectiveCommission,
        decimal netPayout)
    {
        PaidPrice = paidPrice;
        Installment = installment;
        ProviderCost = providerCost;
        GatewayMargin = gatewayMargin;
        TotalEffectiveCommission = totalEffectiveCommission;
        NetPayout = netPayout;
    }

    /// <summary>İşlemin ödenen tutarı (girdi).</summary>
    public decimal PaidPrice { get; }

    /// <summary>Taksit sayısı (girdi, bilgi amaçlı).</summary>
    public int Installment { get; }

    /// <summary>iyzico maliyeti = ProviderCommission + ProviderFee (ayrıştırılmış toplam).</summary>
    public decimal ProviderCost { get; }

    /// <summary>Gateway marjı = PaidPrice·RatePercent + FixedFee (2 ondalık yuvarlı).</summary>
    public decimal GatewayMargin { get; }

    /// <summary>Toplam efektif komisyon = ProviderCost + GatewayMargin.</summary>
    public decimal TotalEffectiveCommission { get; }

    /// <summary>Merchant net hakediş = PaidPrice − TotalEffectiveCommission.</summary>
    public decimal NetPayout { get; }

    /// <summary>Hesap-sonucu fabrikası (aritmetiği çağıran aggregate metodu yapar).</summary>
    public static EffectiveCommission Create(
        decimal paidPrice,
        int installment,
        decimal providerCost,
        decimal gatewayMargin,
        decimal totalEffectiveCommission,
        decimal netPayout) =>
        new(paidPrice, installment, providerCost, gatewayMargin, totalEffectiveCommission, netPayout);
}
