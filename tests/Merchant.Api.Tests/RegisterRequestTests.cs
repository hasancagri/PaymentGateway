using Merchant.Api.Domains.RegisterRequests;
using Merchant.Api.Domains.RegisterRequests.ValueObjects;
using Merchant.Api.Domains.DomainControlChallenges;
using Xunit;

namespace Merchant.Api.Tests;

public class RegisterRequestTests
{
    private static MerchantDescriptor ValidDescriptor() =>
        MerchantDescriptor.Create("1.0", "shop.example.com", "Örnek A.Ş.", "1234567890",
            "onboarding@example.com", "https://shop.example.com/webhook", null).Data!;

    [Fact]
    public void Create_challenge_Passed_ile_Pending_talep_olusur()
    {
        var result = RegisterRequest.Create("shop.example.com", ValidDescriptor(), ChallengeOutcome.Passed);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Pending, result.Data!.Status);
        Assert.Equal("shop.example.com", result.Data.Domain);
        Assert.Equal("Örnek A.Ş.", result.Data.LegalName);
    }

    [Theory]
    [InlineData(ChallengeOutcome.Pending)]
    [InlineData(ChallengeOutcome.Failed)]
    [InlineData(ChallengeOutcome.Expired)]
    public void Create_challenge_Passed_degilse_talep_olusmaz(ChallengeOutcome outcome)
    {
        var result = RegisterRequest.Create("shop.example.com", ValidDescriptor(), outcome);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_domain_normalize_edilir()
    {
        var result = RegisterRequest.Create("  SHOP.Example.COM ", ValidDescriptor(), ChallengeOutcome.Passed);

        Assert.True(result.IsSuccess);
        Assert.Equal("shop.example.com", result.Data!.Domain);
    }

    [Fact]
    public void Approve_yalniz_Pending_calisir_ve_merchant_baglar()
    {
        var req = RegisterRequest.Create("shop.example.com", ValidDescriptor(), ChallengeOutcome.Passed).Data!;
        var merchantId = Guid.NewGuid();

        var approve = req.Approve(merchantId, "ok");

        Assert.True(approve.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Approved, req.Status);
        Assert.Equal(merchantId, req.CreatedMerchantId);
    }

    [Fact]
    public void Approve_iki_kez_ikinci_RET()
    {
        var req = RegisterRequest.Create("shop.example.com", ValidDescriptor(), ChallengeOutcome.Passed).Data!;
        req.Approve(Guid.NewGuid(), null);

        var second = req.Approve(Guid.NewGuid(), null);

        Assert.False(second.IsSuccess);
    }

    [Fact]
    public void Reject_yalniz_Pending_calisir()
    {
        var req = RegisterRequest.Create("shop.example.com", ValidDescriptor(), ChallengeOutcome.Passed).Data!;

        var reject = req.Reject("eksik belge");

        Assert.True(reject.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Rejected, req.Status);
    }

    [Fact]
    public void Rejected_talep_tekrar_Approve_edilemez()
    {
        var req = RegisterRequest.Create("shop.example.com", ValidDescriptor(), ChallengeOutcome.Passed).Data!;
        req.Reject(null);

        var approve = req.Approve(Guid.NewGuid(), null);

        Assert.False(approve.IsSuccess);
    }

    [Fact]
    public void Descriptor_eksik_alan_reddedilir()
    {
        var missing = MerchantDescriptor.Create("1.0", "shop.example.com", "", "1234567890",
            "onboarding@example.com", "https://shop.example.com/webhook", null);

        Assert.False(missing.IsSuccess);
    }

    [Fact]
    public void Descriptor_webhook_https_degilse_reddedilir()
    {
        var http = MerchantDescriptor.Create("1.0", "shop.example.com", "Örnek", "1234567890",
            "onboarding@example.com", "http://shop.example.com/webhook", null);

        Assert.False(http.IsSuccess);
    }
}
