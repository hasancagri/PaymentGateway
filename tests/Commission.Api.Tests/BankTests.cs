using Commission.Api.Domains.Banks;
using Xunit;

namespace Commission.Api.Tests;

public class BankTests
{
    private const string Code = "0062";
    private const string Name = "Garanti BBVA"; // artık Reference read-model'den handler'ca geçilir
    private static readonly int[] ValidInstallments = { 1, 2, 3, 6 };

    [Fact]
    public void Create_gecerli_kod_ve_ad_Ok()
    {
        var result = Bank.Create(Code, Name, ValidInstallments);

        Assert.True(result.IsSuccess);
        Assert.Equal(Code, result.Data!.Code);
        Assert.Equal(Name, result.Data.Name);
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
        var result = Bank.Create(code, Name, ValidInstallments);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Bank.Code) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ad_bos_katalogda_yok_Error(string name)
    {
        // Boş ad = banka Reference kataloğunda yok (handler read-model'de bulamadı).
        var result = Bank.Create(Code, name, ValidInstallments);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Bank.Code) &&
            m.Code == Commission.Api.Domains.SharedKernel.CommissionResourceConstants.BANK_NOT_IN_CATALOG);
    }

    [Fact]
    public void Create_installments_bos_Error()
    {
        var result = Bank.Create(Code, Name, Array.Empty<int>());

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
        var result = Bank.Create(Code, Name, new[] { 1, bad });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Bank.SupportedInstallments) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE);
    }

    [Fact]
    public void Create_installments_tekillestir_ve_sirala()
    {
        var result = Bank.Create(Code, Name, new[] { 6, 1, 3, 1, 2, 6 });

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 1, 2, 3, 6 }, result.Data!.SupportedInstallments);
    }

    [Fact]
    public void Update_code_ve_ad_degismez_aktiflik_taksit_guncellenir()
    {
        var bank = Bank.Create(Code, Name, ValidInstallments).Data!;

        var result = bank.Update(isActive: false, new[] { 1, 2 });

        Assert.True(result.IsSuccess);
        Assert.Equal(Code, bank.Code);
        Assert.Equal(Name, bank.Name);
        Assert.False(bank.IsActive);
        Assert.Equal(new[] { 1, 2 }, bank.SupportedInstallments);
        Assert.NotNull(bank.UpdatedTime);
    }

    [Fact]
    public void SoftDelete_bayrak_ve_zaman()
    {
        var bank = Bank.Create(Code, Name, ValidInstallments).Data!;

        bank.SoftDelete();

        Assert.True(bank.IsDeleted);
        Assert.NotNull(bank.DeletedTime);
    }
}