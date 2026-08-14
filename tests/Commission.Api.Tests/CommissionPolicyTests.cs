using Commission.Api.Domains.CommissionPolicies;
using Xunit;

namespace Commission.Api.Tests;

public class CommissionPolicyTests
{
    private static readonly (decimal, decimal, decimal)[] DefaultTariff = [(0m, 0.015m, 0.50m)];

    private static CommissionPolicy NewPolicy() =>
        CommissionPolicy.Create(Guid.NewGuid(), DefaultTariff).Data!;

    [Fact]
    public void Create_Gecerli_ActiveDogar()
    {
        var merchantId = Guid.NewGuid();
        var result = CommissionPolicy.Create(merchantId, DefaultTariff);

        Assert.True(result.IsSuccess);
        Assert.Equal(merchantId, result.Data!.MerchantId);
        Assert.Equal(CommissionPolicyStatus.Active, result.Data!.Status);
        Assert.Equal(0.015m, result.Data!.Margin.Tiers[0].RatePercent);
    }

    [Fact]
    public void Create_BosMerchantId_Reddedilir()
    {
        var result = CommissionPolicy.Create(Guid.Empty, DefaultTariff);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_GecersizTarife_Reddedilir()
    {
        var result = CommissionPolicy.Create(Guid.NewGuid(), [(0m, -0.1m, 0m)]);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void UpdateMargin_Gecerli_YeniTarifeYururlukte()
    {
        var policy = NewPolicy();

        var result = policy.UpdateMargin([(0m, 0.02m, 1m), (1000m, 0.018m, 0m)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, policy.Margin.Tiers.Count);
        Assert.Equal(0.02m, policy.Margin.Tiers[0].RatePercent);
    }

    [Fact]
    public void UpdateMargin_GecersizTablo_EskiTarifeKorunur()
    {
        var policy = NewPolicy();

        var result = policy.UpdateMargin([(500m, 0.02m, 0m)]); // ilk kademe 0 değil

        Assert.False(result.IsSuccess);
        Assert.Single(policy.Margin.Tiers);
        Assert.Equal(0.015m, policy.Margin.Tiers[0].RatePercent);
    }

    [Fact]
    public void ChangeStatus_FarkliStatu_Degisir()
    {
        var policy = NewPolicy();

        var result = policy.ChangeStatus(CommissionPolicyStatus.Passive);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.Equal(CommissionPolicyStatus.Passive, policy.Status);
    }

    [Fact]
    public void ChangeStatus_AyniStatu_IdempotentNoOp()
    {
        var policy = NewPolicy();

        var result = policy.ChangeStatus(CommissionPolicyStatus.Active);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data); // degismedi
        Assert.Equal(CommissionPolicyStatus.Active, policy.Status);
    }
}
