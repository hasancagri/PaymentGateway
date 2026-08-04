using Merchant.Api.Domains.SettlementAccounts;
using Xunit;

namespace Merchant.Api.Tests;

public class SettlementAccountTests
{
    // Geçerli TR IBAN (ISO 13616 mod-97 == 1).
    private const string ValidIban = "TR460010000000000000000001";
    private static readonly Guid ValidMerchantId = Guid.NewGuid();
    private const string ValidBankCode = "0010";
    private const string ValidOwner = "ACME Ltd. Şti.";

    private static ResultDomain<SettlementAccount> CreateValid() =>
        SettlementAccount.Create(ValidMerchantId, ValidBankCode, ValidIban, ValidOwner, "123", "TL hesabı");

    [Fact]
    public void Create_gecerli_bilgilerle_Ok_doner_ve_Active_baslar()
    {
        var result = CreateValid();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(SettlementAccountStatus.Active, result.Data!.Status);
        Assert.Equal(ValidIban, result.Data.Iban);
    }

    [Fact]
    public void Create_bosluklu_IBAN_normalize_edilip_Ok()
    {
        var result = SettlementAccount.Create(
            ValidMerchantId, ValidBankCode, "tr46 0010 0000 0000 0000 0000 01", ValidOwner, "123", "");

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidIban, result.Data!.Iban);
    }

    [Fact]
    public void Create_bozuk_mod97_IBAN_Error_INVALID_FORMAT()
    {
        // Doğru biçim ama yanlış kontrol basamağı (47 yerine 46 geçerliydi).
        var result = SettlementAccount.Create(
            ValidMerchantId, ValidBankCode, "TR470010000000000000000001", ValidOwner, "123", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(SettlementAccount.Iban) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Fact]
    public void Create_TR_disi_IBAN_Error_INVALID_FORMAT()
    {
        // Geçerli mod-97 Alman IBAN'ı; TR kısıtı reddeder.
        var result = SettlementAccount.Create(
            ValidMerchantId, ValidBankCode, "DE89370400440532013000", ValidOwner, "123", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(SettlementAccount.Iban) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Fact]
    public void Create_bos_merchantId_Error_required()
    {
        var result = SettlementAccount.Create(
            Guid.Empty, ValidBankCode, ValidIban, ValidOwner, "123", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(SettlementAccount.MerchantId) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_bos_bankCode_Error_required(string bankCode)
    {
        var result = SettlementAccount.Create(
            ValidMerchantId, bankCode, ValidIban, ValidOwner, "123", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(SettlementAccount.BankCode) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Fact]
    public void Create_bos_iban_Error_required()
    {
        var result = SettlementAccount.Create(
            ValidMerchantId, ValidBankCode, "", ValidOwner, "123", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(SettlementAccount.Iban) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Fact]
    public void Create_bos_ownerName_Error_required()
    {
        var result = SettlementAccount.Create(
            ValidMerchantId, ValidBankCode, ValidIban, "", "123", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(SettlementAccount.AccountOwnerName) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Theory]
    [InlineData("001")]
    [InlineData("00100")]
    [InlineData("abcd")]
    public void Create_gecersiz_bankCode_format_Error_INVALID_FORMAT(string bankCode)
    {
        var result = SettlementAccount.Create(
            ValidMerchantId, bankCode, ValidIban, ValidOwner, "123", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(SettlementAccount.BankCode) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Fact]
    public void UpdateDetails_gecerli_bilgilerle_alanlari_gunceller()
    {
        var account = CreateValid().Data!;
        const string yeniIban = "TR950006400000000000000099";

        var result = account.UpdateDetails("0064", yeniIban, "Yeni Sahip", "999", "yeni");

        Assert.True(result.IsSuccess);
        Assert.Equal("0064", account.BankCode);
        Assert.Equal(yeniIban, account.Iban);
        Assert.Equal("Yeni Sahip", account.AccountOwnerName);
    }

    [Fact]
    public void UpdateDetails_bozuk_IBAN_Error_ve_alanlar_degismez()
    {
        var account = CreateValid().Data!;

        var result = account.UpdateDetails(ValidBankCode, "TR470010000000000000000001", "Yeni", "9", "");

        Assert.False(result.IsSuccess);
        // Eski değerler korunur.
        Assert.Equal(ValidIban, account.Iban);
        Assert.Equal(ValidOwner, account.AccountOwnerName);
    }

    [Fact]
    public void Deactivate_Passive_yapar_ve_kayit_korunur()
    {
        var account = CreateValid().Data!;

        account.Deactivate();

        Assert.Equal(SettlementAccountStatus.Passive, account.Status);
        Assert.False(account.IsActive);
        Assert.False(account.IsDeleted);
    }

    [Fact]
    public void Deactivate_sonra_Activate_Active_yapar()
    {
        var account = CreateValid().Data!;

        account.Deactivate();
        account.Activate();

        Assert.Equal(SettlementAccountStatus.Active, account.Status);
        Assert.True(account.IsActive);
    }
}