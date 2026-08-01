using Merchant.Api.Domains.Merchants;
using MerchantAggregate = Merchant.Api.Domains.Merchants.Merchant;
using Xunit;

namespace Merchant.Api.Tests;

public class MerchantTests
{
    private static readonly string ValidKey = "mk_9f1c2a7b8d3e4f5061728394a5b6c7d8";
    private static readonly string ValidName = "Acme Ltd";
    private static readonly string ValidEmail = "ops@acme.com";
    private static readonly string ValidPhone = "+902121234567";
    private static readonly string ValidCountry = "TR";
    private static readonly string ValidCity = "34";
    private static readonly string ValidMcc = "5411";
    private static readonly string ValidWebhook = "https://acme.com/webhooks/payments";

    private static ResultDomain<MerchantAggregate> CreateValid() =>
        MerchantAggregate.Create(ValidKey, ValidName, ValidEmail, ValidPhone, ValidCountry, ValidCity, ValidMcc, ValidWebhook);

    [Fact]
    public void Create_gecerli_bilgilerle_Ok_doner_ve_Active_baslar()
    {
        var result = CreateValid();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(MerchantStatus.Active, result.Data!.Status);
        Assert.Equal(ValidMcc, result.Data.Mcc);
        Assert.Equal(ValidKey, result.Data.MerchantKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_bos_merchantKey_Error_required(string key)
    {
        var result = MerchantAggregate.Create(key, ValidName, ValidEmail, ValidPhone, ValidCountry, ValidCity, ValidMcc, ValidWebhook);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(MerchantAggregate.MerchantKey) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Fact]
    public void MerchantKey_UpdateProfile_sonrasi_degismez()
    {
        var merchant = CreateValid().Data!;

        merchant.UpdateProfile("Yeni Ad", "yeni@acme.com", "+902129998877",
            ValidCountry, ValidCity, "5812", "https://acme.com/wh2");

        Assert.Equal(ValidKey, merchant.MerchantKey);
    }

    [Fact]
    public void MerchantKey_status_gecislerinde_degismez()
    {
        var merchant = CreateValid().Data!;

        merchant.Deactivate();
        merchant.Suspend();
        merchant.Activate();

        Assert.Equal(ValidKey, merchant.MerchantKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_bos_isim_Error(string name)
    {
        var result = MerchantAggregate.Create(ValidKey, name, ValidEmail, ValidPhone, ValidCountry, ValidCity, ValidMcc, ValidWebhook);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Property == nameof(MerchantAggregate.Name));
    }

    [Fact]
    public void Create_bos_email_Error_required()
    {
        var result = MerchantAggregate.Create(ValidKey, ValidName, "", ValidPhone, ValidCountry, ValidCity, ValidMcc, ValidWebhook);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(MerchantAggregate.Email) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@no-local.com")]
    public void Create_gecersiz_email_format_Error(string email)
    {
        var result = MerchantAggregate.Create(ValidKey, ValidName, email, ValidPhone, ValidCountry, ValidCity, ValidMcc, ValidWebhook);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(MerchantAggregate.Email) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Theory]
    [InlineData("541")]
    [InlineData("54111")]
    [InlineData("abcd")]
    [InlineData("")]
    public void Create_gecersiz_MCC_format_Error(string mcc)
    {
        var result = MerchantAggregate.Create(ValidKey, ValidName, ValidEmail, ValidPhone, ValidCountry, ValidCity, mcc, ValidWebhook);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(MerchantAggregate.Mcc) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Theory]
    [InlineData("acme.com/webhook")]
    [InlineData("ftp://acme.com/wh")]
    [InlineData("/relative/path")]
    public void Create_gecersiz_webhook_url_Error(string webhook)
    {
        var result = MerchantAggregate.Create(ValidKey, ValidName, ValidEmail, ValidPhone, ValidCountry, ValidCity, ValidMcc, webhook);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(MerchantAggregate.WebhookUrl) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Fact]
    public void Suspend_durumu_Suspended_yapar_ve_pasiflestirir()
    {
        var merchant = CreateValid().Data!;

        merchant.Suspend();

        Assert.Equal(MerchantStatus.Suspended, merchant.Status);
        Assert.False(merchant.IsActive);
    }

    [Fact]
    public void Deactivate_sonra_Activate_durum_gecisleri()
    {
        var merchant = CreateValid().Data!;

        merchant.Deactivate();
        Assert.Equal(MerchantStatus.Passive, merchant.Status);
        Assert.False(merchant.IsActive);

        merchant.Activate();
        Assert.Equal(MerchantStatus.Active, merchant.Status);
        Assert.True(merchant.IsActive);
    }
}