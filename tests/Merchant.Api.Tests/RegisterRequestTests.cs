using Merchant.Api.Domains.RegisterRequests;
using Merchant.Api.Domains.RegisterRequests.ValueObjects;
using Xunit;

namespace Merchant.Api.Tests;

public class RegisterRequestTests
{
    private static MerchantDescriptor ValidDescriptor() =>
        MerchantDescriptor.Create("1.0", "shop.example.com", "Örnek A.Ş.", "1234567890",
            "onboarding@example.com", "https://shop.example.com/webhook", null).Data!;

    private static RegisterRequest AwaitingRequest() =>
        RegisterRequest.CreateAwaiting("shop.example.com", ValidDescriptor()).Data!;

    // Challenge geçmiş (Pending) talep — VerifyChallenge doğru değerle çağrılır.
    private static RegisterRequest PendingRequest()
    {
        var req = AwaitingRequest();
        req.VerifyChallenge(req.ChallengeExpectedValue, DateTime.UtcNow);
        return req;
    }

    // --- CreateAwaiting + challenge (015: DomainControlChallengeTests'ten taşındı) ---

    [Fact]
    public void CreateAwaiting_AwaitingDomainControl_ve_challenge_uretir()
    {
        var req = AwaitingRequest();

        Assert.Equal(RegisterRequestStatus.AwaitingDomainControl, req.Status);
        Assert.Equal(ChallengeOutcome.Pending, req.ChallengeResult);
        Assert.False(string.IsNullOrWhiteSpace(req.ChallengeToken));
        Assert.False(string.IsNullOrWhiteSpace(req.ChallengeExpectedValue));
        Assert.Equal("shop.example.com", req.Domain);
        Assert.Equal("Örnek A.Ş.", req.LegalName);
    }

    [Fact]
    public void CreateAwaiting_domain_normalize_edilir()
    {
        var req = RegisterRequest.CreateAwaiting("  SHOP.Example.COM ", ValidDescriptor()).Data!;

        Assert.Equal("shop.example.com", req.Domain);
    }

    [Fact]
    public void VerifyChallenge_dogru_deger_Passed_ve_Pending()
    {
        var req = AwaitingRequest();

        var outcome = req.VerifyChallenge(req.ChallengeExpectedValue, DateTime.UtcNow).Data!;

        Assert.Equal(ChallengeOutcome.Passed, outcome);
        Assert.Equal(RegisterRequestStatus.Pending, req.Status);
    }

    [Fact]
    public void VerifyChallenge_yanlis_deger_Failed_ve_AwaitingDomainControl_kalir()
    {
        var req = AwaitingRequest();

        var outcome = req.VerifyChallenge("yanlis", DateTime.UtcNow).Data!;

        Assert.Equal(ChallengeOutcome.Failed, outcome);
        Assert.Equal(RegisterRequestStatus.AwaitingDomainControl, req.Status);
    }

    [Fact]
    public void VerifyChallenge_null_deger_Failed()
    {
        var req = AwaitingRequest();

        Assert.Equal(ChallengeOutcome.Failed, req.VerifyChallenge(null, DateTime.UtcNow).Data!);
    }

    [Fact]
    public void VerifyChallenge_sure_dolmus_Expired()
    {
        var req = AwaitingRequest();

        var outcome = req.VerifyChallenge(
            req.ChallengeExpectedValue, DateTime.UtcNow.AddHours(RegisterRequest.ChallengeTtlHours + 1)).Data!;

        Assert.Equal(ChallengeOutcome.Expired, outcome);
        Assert.Equal(RegisterRequestStatus.AwaitingDomainControl, req.Status);
    }

    [Fact]
    public void VerifyChallenge_Passed_sonrasi_tekrar_Passed_doner()
    {
        var req = PendingRequest();

        // Tek-kullanım idempotent: tekrar Verify durumu değiştirmez, Passed sabit kalır.
        var again = req.VerifyChallenge("baska", DateTime.UtcNow).Data!;

        Assert.Equal(ChallengeOutcome.Passed, again);
        Assert.Equal(RegisterRequestStatus.Pending, req.Status);
    }

    [Fact]
    public void IssueChallenge_yeni_token_ve_deger_uretir()
    {
        var req = AwaitingRequest();
        var oldToken = req.ChallengeToken;
        var oldValue = req.ChallengeExpectedValue;

        req.IssueChallenge(DateTime.UtcNow);

        Assert.NotEqual(oldToken, req.ChallengeToken);
        Assert.NotEqual(oldValue, req.ChallengeExpectedValue);
        Assert.Equal(ChallengeOutcome.Pending, req.ChallengeResult);
    }

    // --- Karar kapıları (Approve/Reject) ---

    [Fact]
    public void Approve_AwaitingDomainControl_talepte_RET()
    {
        var req = AwaitingRequest();

        var approve = req.Approve(Guid.NewGuid(), null);

        Assert.False(approve.IsSuccess);
    }

    [Fact]
    public void Approve_yalniz_Pending_calisir_ve_merchant_baglar()
    {
        var req = PendingRequest();
        var merchantId = Guid.NewGuid();

        var approve = req.Approve(merchantId, "ok");

        Assert.True(approve.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Approved, req.Status);
        Assert.Equal(merchantId, req.CreatedMerchantId);
    }

    [Fact]
    public void Approve_iki_kez_ikinci_RET()
    {
        var req = PendingRequest();
        req.Approve(Guid.NewGuid(), null);

        var second = req.Approve(Guid.NewGuid(), null);

        Assert.False(second.IsSuccess);
    }

    [Fact]
    public void Reject_yalniz_Pending_calisir()
    {
        var req = PendingRequest();

        var reject = req.Reject("eksik belge");

        Assert.True(reject.IsSuccess);
        Assert.Equal(RegisterRequestStatus.Rejected, req.Status);
    }

    [Fact]
    public void Rejected_talep_tekrar_Approve_edilemez()
    {
        var req = PendingRequest();
        req.Reject(null);

        var approve = req.Approve(Guid.NewGuid(), null);

        Assert.False(approve.IsSuccess);
    }

    // --- Descriptor doğrulama (değişmez) ---

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