
namespace Merchant.Api.Tests;

public class RegisterRequestTests
{
    private static RegisterRequest PendingRequest() =>
        RegisterRequest.CreatePending("shop.example.com", "Örnek A.Ş.", "1234567890",
            "onboarding@example.com", "https://shop.example.com/webhook").Data!;

    // --- CreatePending (challenge yok — alanlar doğrulanınca doğrudan Pending) ---

    [Fact]
    public void CreatePending_Pending_statusunde_dogar()
    {
        var req = PendingRequest();

        Assert.Equal(RegisterRequestStatus.Pending, req.Status);
        Assert.Equal("shop.example.com", req.Domain);
        Assert.Equal("Örnek A.Ş.", req.LegalName);
        Assert.Equal("1234567890", req.TaxId);
    }

    [Fact]
    public void CreatePending_domain_normalize_edilir()
    {
        var req = RegisterRequest.CreatePending("  SHOP.Example.COM ", "Örnek A.Ş.", "1234567890",
            "onboarding@example.com", "https://shop.example.com/webhook").Data!;

        Assert.Equal("shop.example.com", req.Domain);
    }

    [Fact]
    public void CreatePending_bos_domain_reddedilir()
    {
        var req = RegisterRequest.CreatePending("  ", "Örnek A.Ş.", "1234567890",
            "onboarding@example.com", "https://shop.example.com/webhook");

        Assert.False(req.IsSuccess);
    }

    // --- Karar kapıları (Approve/Reject) ---

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

    // --- Alan doğrulama (push-inline — CreatePending içinde) ---

    [Fact]
    public void CreatePending_eksik_alan_reddedilir()
    {
        var missing = RegisterRequest.CreatePending("shop.example.com", "", "1234567890",
            "onboarding@example.com", "https://shop.example.com/webhook");

        Assert.False(missing.IsSuccess);
    }

    [Fact]
    public void CreatePending_gecersiz_email_reddedilir()
    {
        var badEmail = RegisterRequest.CreatePending("shop.example.com", "Örnek", "1234567890",
            "gecersiz-email", "https://shop.example.com/webhook");

        Assert.False(badEmail.IsSuccess);
    }

    [Fact]
    public void CreatePending_webhook_https_degilse_reddedilir()
    {
        var http = RegisterRequest.CreatePending("shop.example.com", "Örnek", "1234567890",
            "onboarding@example.com", "http://shop.example.com/webhook");

        Assert.False(http.IsSuccess);
    }
}
