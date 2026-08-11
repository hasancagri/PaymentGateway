using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;

namespace Merchant.Api.Tests;

public class MerchantTests
{
    // Doğrudan Merchant.Create fabrikası SİLİNDİ — merchant yalnız onboarding onayıyla doğar
    // (CreateForOnboarding; validasyon testleri MerchantOnboardingTests'te). Durum-geçiş testleri
    // onboarding fabrikası üzerinden sürer.
    private static readonly string ValidKey = "mk_9f1c2a7b8d3e4f5061728394a5b6c7d8";
    private static readonly string ValidName = "Acme Ltd";
    private static readonly string ValidEmail = "ops@acme.com";
    private static readonly string ValidWebhook = "https://acme.com/webhooks/payments";

    [Fact]
    public void MerchantKey_status_gecislerinde_degismez()
    {
        var merchant = OnboardingMerchant();

        merchant.Deactivate();
        merchant.Suspend();
        merchant.Activate();

        Assert.Equal(ValidKey, merchant.MerchantKey);
    }

    [Fact]
    public void Suspend_durumu_Suspended_yapar_ve_pasiflestirir()
    {
        var merchant = OnboardingMerchant();

        merchant.Suspend();

        Assert.Equal(MerchantStatus.Suspended, merchant.Status);
        Assert.False(merchant.IsActive);
    }

    [Fact]
    public void Activate_yalniz_Provisioning_den()
    {
        var merchant = OnboardingMerchant();

        var activate = merchant.Activate();
        Assert.True(activate.IsSuccess);
        Assert.Equal(MerchantStatus.Active, merchant.Status);
        Assert.True(merchant.IsActive);

        merchant.Deactivate();
        Assert.Equal(MerchantStatus.Passive, merchant.Status);
        Assert.False(merchant.IsActive);

        // 2026-08-11 kuralı: Passive'den Activate geri dönüşü yok.
        Assert.False(merchant.Activate().IsSuccess);
        Assert.Equal(MerchantStatus.Passive, merchant.Status);
    }

    // --- Aktivasyon bileti (015: ActivationTicketTests'ten taşındı; davranış Merchant üstünde) ---

    private static MerchantAggregate OnboardingMerchant() =>
        MerchantAggregate.CreateForOnboarding(ValidKey, ValidName, ValidEmail, ValidWebhook, "1234567890", null).Data!;

    [Fact]
    public void IssueActivation_token_ve_sure_uretir_kullanilmamis()
    {
        var m = OnboardingMerchant();

        m.IssueActivation(DateTime.UtcNow);

        Assert.False(string.IsNullOrWhiteSpace(m.ActivationToken));
        Assert.NotNull(m.ActivationExpiresAtUtc);
        Assert.Null(m.ActivationRedeemedAtUtc);
    }

    [Fact]
    public void RedeemActivation_ilk_kez_Ok_Redeemed_ve_Provision()
    {
        var m = OnboardingMerchant();
        m.IssueActivation(DateTime.UtcNow);

        var r = m.RedeemActivation(DateTime.UtcNow);

        Assert.True(r.IsSuccess);
        Assert.NotNull(m.ActivationRedeemedAtUtc);
        Assert.Equal(MerchantStatus.Provisioning, m.Status);
        Assert.NotNull(m.ActivatedAtUtc);
    }

    [Fact]
    public void RedeemActivation_ikinci_kez_RET()
    {
        var m = OnboardingMerchant();
        m.IssueActivation(DateTime.UtcNow);
        m.RedeemActivation(DateTime.UtcNow);

        var second = m.RedeemActivation(DateTime.UtcNow);

        Assert.False(second.IsSuccess);
    }

    [Fact]
    public void RedeemActivation_sure_dolmus_RET()
    {
        var m = OnboardingMerchant();
        m.IssueActivation(DateTime.UtcNow);

        var r = m.RedeemActivation(DateTime.UtcNow.AddHours(MerchantAggregate.ActivationTtlHours + 1));

        Assert.False(r.IsSuccess);
        Assert.Null(m.ActivationRedeemedAtUtc);
    }

    [Fact]
    public void RedeemActivation_bilet_yokken_RET()
    {
        var m = OnboardingMerchant();

        var r = m.RedeemActivation(DateTime.UtcNow);

        Assert.False(r.IsSuccess);
    }
}