using Commission.Api.Domains.CommissionPolicies;
using Xunit;

namespace Commission.Api.Tests;

public class EffectiveCommissionTests
{
    // marj: tek kademe — oran %1.5 + 0.50 TL sabit (030 öncesi düz modelin birebir karşılığı)
    private static CommissionPolicy Policy(decimal rate = 0.015m, decimal fee = 0.50m) =>
        CommissionPolicy.Create(Guid.NewGuid(), [(0m, rate, fee)]).Data!;

    // 030 spec tarifesi: 0+ %2,5+1 · 1.000+ %2+1 · 10.000+ %1,8+0
    private static CommissionPolicy TieredPolicy() =>
        CommissionPolicy.Create(Guid.NewGuid(),
            [(0m, 0.025m, 1m), (1000m, 0.02m, 1m), (10000m, 0.018m, 0m)]).Data!;

    [Fact]
    public void Calculate_BilinenDegerler_ElleAritmetigeEsit_SC002()
    {
        var policy = Policy();

        var result = policy.CalculateEffectiveCommission(1000.00m, "18.50", "0.25", 3);

        Assert.True(result.IsSuccess);
        var ec = result.Data!;
        Assert.Equal(18.75m, ec.IyzicoCost);              // 18.50 + 0.25
        Assert.Equal(15.50m, ec.GatewayMargin);           // 1000*0.015 + 0.50
        Assert.Equal(34.25m, ec.TotalEffectiveCommission); // 18.75 + 15.50
        Assert.Equal(965.75m, ec.NetPayout);              // 1000 - 34.25
    }

    // --- 030: kademeli marj vektörleri (spec SC-002) ---

    [Fact]
    public void Calculate_KademeIci_IlkKademedenMarj()
    {
        var result = TieredPolicy().CalculateEffectiveCommission(500.00m, "2.50", "0.25", 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(13.50m, result.Data!.GatewayMargin); // 500*0.025 + 1
    }

    [Fact]
    public void Calculate_TamSinir_UstKademedenMarj()
    {
        var result = TieredPolicy().CalculateEffectiveCommission(1000.00m, "2.50", "0.25", 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(21.00m, result.Data!.GatewayMargin); // 1000*0.02 + 1
    }

    [Fact]
    public void Calculate_AcikUcluSonKademe_Marj()
    {
        var result = TieredPolicy().CalculateEffectiveCommission(20000.00m, "500.00", "0.25", 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(360.00m, result.Data!.GatewayMargin); // 20000*0.018 + 0
    }

    [Fact]
    public void Calculate_TekKademeliTablo_DuzModelleBirebir_SC004()
    {
        // 029 canlı senaryosundaki düz tarife (0.02 + 1): 100 TL → marj 3.00
        var policy = Policy(0.02m, 1m);

        var result = policy.CalculateEffectiveCommission(100.00m, "2.50", "0.25", 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(3.00m, result.Data!.GatewayMargin);
    }

    [Fact]
    public void Calculate_Yuvarlama_2OndalikAwayFromZero_SC002()
    {
        // 100 * 0.015 = 1.5 ; + 0.005 fee -> 1.505 -> AwayFromZero -> 1.51
        var policy = Policy(0.015m, 0.005m);

        var result = policy.CalculateEffectiveCommission(100.00m, "1.00", "0.00", 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(1.51m, result.Data!.GatewayMargin);
    }

    [Fact]
    public void Calculate_PasifPolitika_Reddedilir()
    {
        var policy = Policy();
        policy.ChangeStatus(CommissionPolicyStatus.Passive);

        var result = policy.CalculateEffectiveCommission(1000m, "10", "1", 1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Calculate_AyristirilamazMaliyet_Reddedilir_FR012()
    {
        var policy = Policy();

        var result = policy.CalculateEffectiveCommission(1000m, "abc", "1", 1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Calculate_EfektifKomisyonTutariAsar_Reddedilir_SC005()
    {
        // paidPrice 10 ; iyzico 9.50 + 1.00 = 10.50 > 10 -> reddedilir (negatif hakedis yok)
        var policy = Policy(0.02m, 1m);

        var result = policy.CalculateEffectiveCommission(10.00m, "9.50", "1.00", 1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Calculate_NegatifMaliyet_Reddedilir()
    {
        var policy = Policy();

        var result = policy.CalculateEffectiveCommission(1000m, "-5", "1", 1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Calculate_SifirPaidPrice_Reddedilir()
    {
        var policy = Policy();

        var result = policy.CalculateEffectiveCommission(0m, "1", "1", 1);

        Assert.False(result.IsSuccess);
    }
}
