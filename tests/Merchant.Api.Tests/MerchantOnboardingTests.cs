using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;

namespace Merchant.Api.Tests;

public class MerchantOnboardingTests
{
    private static MerchantAggregate Onboard() =>
        MerchantAggregate.CreateForOnboarding(
            "mk_9f1c2a7b8d3e4f5061728394a5b6c7d8", "Acme Ltd", "ops@acme.com",
            "https://acme.com/webhook", "1234567890", null).Data!;

    [Fact]
    public void CreateForOnboarding_Provisioning_baslar_charge_yok()
    {
        var m = Onboard();

        Assert.Equal(MerchantStatus.Provisioning, m.Status);
        Assert.False(m.IsActive);
    }

    [Fact]
    public void CreateForOnboarding_https_olmayan_webhook_reddedilir()
    {
        var r = MerchantAggregate.CreateForOnboarding(
            "mk_x", "Acme", "ops@acme.com", "http://acme.com/webhook", null, null);

        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void RedeemActivation_ActivatedAt_set_eder_Provisioning_kalir()
    {
        var m = Onboard();
        m.IssueActivation(DateTime.UtcNow);

        m.RedeemActivation(DateTime.UtcNow);

        Assert.Equal(MerchantStatus.Provisioning, m.Status);
        Assert.NotNull(m.ActivatedAtUtc);
    }

    [Fact]
    public void SetReturnUrl_https_zorunlu()
    {
        var m = Onboard();

        Assert.False(m.SetReturnUrl("http://acme.com/return").IsSuccess);
        Assert.True(m.SetReturnUrl("https://acme.com/return").IsSuccess);
        Assert.Equal("https://acme.com/return", m.ReturnUrl);
    }

    [Fact]
    public void TryActivate_iki_kosulla_Active_olmaz()
    {
        var m = Onboard();
        m.MarkSettlementAccountPresent();
        m.MarkCommissionGridReady();
        // ReturnUrl eksik

        Assert.False(m.TryActivate().IsSuccess);
        Assert.Equal(MerchantStatus.Provisioning, m.Status);
    }

    [Fact]
    public void TryActivate_uc_kosulla_Active_olur()
    {
        var m = Onboard();
        m.MarkSettlementAccountPresent();
        m.MarkCommissionGridReady();
        m.SetReturnUrl("https://acme.com/return");

        var activated = m.TryActivate();

        Assert.True(activated.IsSuccess);
        Assert.Equal(MerchantStatus.Active, m.Status);
        Assert.True(m.IsActive);
    }

    [Fact]
    public void TryActivate_tekrar_no_op_false()
    {
        var m = Onboard();
        m.MarkSettlementAccountPresent();
        m.MarkCommissionGridReady();
        m.SetReturnUrl("https://acme.com/return");
        m.TryActivate();

        Assert.False(m.TryActivate().IsSuccess);
        Assert.Equal(MerchantStatus.Active, m.Status);
    }

    [Fact]
    public void CreateForOnboarding_externalRef_saklanir()
    {
        var m = MerchantAggregate.CreateForOnboarding(
            "mk_x1", "Acme", "ops@acme.com", "https://acme.com/webhook", null, " ref-1 ").Data!;

        Assert.Equal("ref-1", m.ExternalRef);
    }
}
