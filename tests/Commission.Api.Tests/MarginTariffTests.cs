using Commission.Api.Domains.CommissionPolicies.ValueObjects;
using Common.Utils.Constants;
using Xunit;

namespace Commission.Api.Tests;

// 030: MarginTariff tablo doğrulaması + kademe seçimi (bracket).
public class MarginTariffTests
{
    private static readonly (decimal, decimal, decimal)[] SpecTariff =
    [
        (0m, 0.025m, 1m),
        (1000m, 0.02m, 1m),
        (10000m, 0.018m, 0m)
    ];

    [Fact]
    public void Create_UcKademeliGecerliTablo_Basarili()
    {
        var result = MarginTariff.Create(SpecTariff);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Tiers.Count);
        Assert.Equal(0m, result.Data!.Tiers[0].FromAmount);
        Assert.Equal(0.018m, result.Data!.Tiers[2].RatePercent);
    }

    [Fact]
    public void Create_TekKademe_Basarili()
    {
        var result = MarginTariff.Create([(0m, 0.02m, 1m)]);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Tiers);
    }

    [Fact]
    public void Create_BosTablo_Reddedilir()
    {
        var result = MarginTariff.Create([]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages,
            m => m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Fact]
    public void Create_OnKademedenFazla_Reddedilir()
    {
        var tiers = Enumerable.Range(0, MarginTariff.MaxTierCount + 1)
            .Select(i => ((decimal)(i * 100), 0.02m, 0m)).ToList();

        var result = MarginTariff.Create(tiers);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_IlkKademeSifirdanBaslamiyor_KademeIsaretliHata()
    {
        var result = MarginTariff.Create([(500m, 0.02m, 1m)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == "Tiers[0].FromAmount");
    }

    [Fact]
    public void Create_ArtmayanAltSinir_KademeIsaretliHata()
    {
        var result = MarginTariff.Create([(0m, 0.02m, 1m), (1000m, 0.02m, 1m), (1000m, 0.018m, 0m)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == "Tiers[2].FromAmount");
    }

    [Fact]
    public void Create_KademeOranTavaniAsar_KademeIsaretliHata()
    {
        var result = MarginTariff.Create([(0m, 0.02m, 1m), (1000m, 0.25m, 1m)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == "Tiers[1].RatePercent");
    }

    [Fact]
    public void Create_KademeSabitUcretTavaniAsar_KademeIsaretliHata()
    {
        var result = MarginTariff.Create([(0m, 0.02m, MarginTariff.MaxFixedFee + 1m)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == "Tiers[0].FixedFee");
    }

    [Fact]
    public void Create_NegatifOran_Reddedilir()
    {
        var result = MarginTariff.Create([(0m, -0.01m, 0m)]);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_SinirDegerleri_Basarili()
    {
        Assert.True(MarginTariff.Create([(0m, 0m, 0m)]).IsSuccess);
        Assert.True(MarginTariff.Create(
            [(0m, MarginTariff.MaxRatePercent, MarginTariff.MaxFixedFee)]).IsSuccess);
    }

    // --- ResolveTier (bracket seçimi) ---

    [Fact]
    public void ResolveTier_KademeIci_DogruKademe()
    {
        var tariff = MarginTariff.Create(SpecTariff).Data!;

        Assert.Equal(0.025m, tariff.ResolveTier(500m).RatePercent);
        Assert.Equal(0.02m, tariff.ResolveTier(5000m).RatePercent);
    }

    [Fact]
    public void ResolveTier_TamSinir_UstKademeyeDuser()
    {
        var tariff = MarginTariff.Create(SpecTariff).Data!;

        Assert.Equal(0.02m, tariff.ResolveTier(1000m).RatePercent);
        Assert.Equal(0.018m, tariff.ResolveTier(10000m).RatePercent);
    }

    [Fact]
    public void ResolveTier_AcikUcluSonKademe_BuyukTutar()
    {
        var tariff = MarginTariff.Create(SpecTariff).Data!;

        Assert.Equal(0.018m, tariff.ResolveTier(1_000_000m).RatePercent);
    }
}
