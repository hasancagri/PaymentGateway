using Commission.Api.Domains.CommissionPolicies;
using Xunit;

namespace Commission.Api.Tests;

public class CommissionPolicyTests
{
    private static CommissionPolicy NewPolicy() =>
        CommissionPolicy.Create(Guid.NewGuid(), 0.015m, 0.50m).Data!;

    [Fact]
    public void Create_Gecerli_ActiveDogar()
    {
        var merchantId = Guid.NewGuid();
        var result = CommissionPolicy.Create(merchantId, 0.015m, 0.50m);

        Assert.True(result.IsSuccess);
        Assert.Equal(merchantId, result.Data!.MerchantId);
        Assert.Equal(CommissionPolicyStatus.Active, result.Data!.Status);
        Assert.Equal(0.015m, result.Data!.Margin.RatePercent);
    }

    [Fact]
    public void Create_BosMerchantId_Reddedilir()
    {
        var result = CommissionPolicy.Create(Guid.Empty, 0.015m, 0.50m);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_GecersizMarj_Reddedilir()
    {
        var result = CommissionPolicy.Create(Guid.NewGuid(), -0.1m, 0m);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void UpdateMargin_Gecerli_YuruluregeGirer()
    {
        var policy = NewPolicy();

        var result = policy.UpdateMargin(0.02m, 1m);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.02m, policy.Margin.RatePercent);
        Assert.Equal(1m, policy.Margin.FixedFee);
    }

    [Fact]
    public void UpdateMargin_Gecersiz_EskiMarjKorunur()
    {
        var policy = NewPolicy();

        var result = policy.UpdateMargin(0.99m, 0m);

        Assert.False(result.IsSuccess);
        Assert.Equal(0.015m, policy.Margin.RatePercent);
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
