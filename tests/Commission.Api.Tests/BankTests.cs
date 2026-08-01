using Commission.Api.Domains.Banks;
using Commission.Api.Domains.SharedKernel;
using Xunit;

namespace Commission.Api.Tests;

public class BankTests
{
    private const string CatalogCode = "0062"; // Garanti BBVA — katalogda
    private static readonly int[] ValidInstallments = { 1, 2, 3, 6 };

    [Fact]
    public void Create_katalog_kodu_Ok_ad_katalogdan()
    {
        var result = Bank.Create(CatalogCode, ValidInstallments);

        Assert.True(result.IsSuccess);
        Assert.Equal(CatalogCode, result.Data!.Code);
        Assert.Equal("Garanti BBVA", result.Data.Name); // ad katalogdan türer
        Assert.Equal(new[] { 1, 2, 3, 6 }, result.Data.SupportedInstallments);
        Assert.True(result.Data.IsActive);
    }

    [Theory]
    [InlineData("062")]
    [InlineData("00622")]
    [InlineData("")]
    [InlineData("abcd")]
    public void Create_code_4_hane_degil_Error(string code)
    {
        var result = Bank.Create(code, ValidInstallments);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Bank.Code) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Theory]
    [InlineData("1234")] // 4 hane ama katalogda yok
    [InlineData("0000")]
    public void Create_katalog_disi_kod_Error(string code)
    {
        var result = Bank.Create(code, ValidInstallments);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Bank.Code) &&
            m.Code == CommissionResourceConstants.BANK_NOT_IN_CATALOG);
    }

    [Fact]
    public void Create_installments_bos_Error()
    {
        var result = Bank.Create(CatalogCode, Array.Empty<int>());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Bank.SupportedInstallments) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(-1)]
    public void Create_installment_aralik_disi_Error(int bad)
    {
        var result = Bank.Create(CatalogCode, new[] { 1, bad });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Bank.SupportedInstallments) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE);
    }

    [Fact]
    public void Create_installments_tekillestir_ve_sirala()
    {
        var result = Bank.Create(CatalogCode, new[] { 6, 1, 3, 1, 2, 6 });

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 1, 2, 3, 6 }, result.Data!.SupportedInstallments);
    }

    [Fact]
    public void Update_code_ve_ad_degismez_aktiflik_taksit_guncellenir()
    {
        var bank = Bank.Create(CatalogCode, ValidInstallments).Data!;

        var result = bank.Update(isActive: false, new[] { 1, 2 });

        Assert.True(result.IsSuccess);
        Assert.Equal(CatalogCode, bank.Code);
        Assert.Equal("Garanti BBVA", bank.Name); // ad değişmez
        Assert.False(bank.IsActive);
        Assert.Equal(new[] { 1, 2 }, bank.SupportedInstallments);
        Assert.NotNull(bank.UpdatedTime);
    }

    [Fact]
    public void Update_gecersiz_installments_Error()
    {
        var bank = Bank.Create(CatalogCode, ValidInstallments).Data!;

        var result = bank.Update(isActive: true, new[] { 99 });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE);
    }

    [Fact]
    public void SoftDelete_bayrak_ve_zaman()
    {
        var bank = Bank.Create(CatalogCode, ValidInstallments).Data!;

        bank.SoftDelete();

        Assert.True(bank.IsDeleted);
        Assert.NotNull(bank.DeletedTime);
    }

    [Theory]
    [InlineData("0062", "Garanti BBVA")]
    [InlineData("0010", "Ziraat Bankası")]
    [InlineData("9997", "Iyzico")]
    public void BankCatalog_kod_varsa_ad_doner(string code, string expectedName)
    {
        Assert.True(BankCatalog.TryGetName(code, out var name));
        Assert.Equal(expectedName, name);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("")]
    [InlineData("abcd")]
    public void BankCatalog_kod_yoksa_false(string code)
    {
        Assert.False(BankCatalog.TryGetName(code, out var name));
        Assert.Equal(string.Empty, name);
    }
}