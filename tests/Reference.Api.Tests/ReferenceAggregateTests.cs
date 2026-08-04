using Reference.Api.Domains.Banks;
using Reference.Api.Domains.Cities;
using Reference.Api.Domains.Countries;
using Reference.Api.Domains.Mccs;
using Xunit;

namespace Reference.Api.Tests;

public class CountryTests
{
    [Fact]
    public void Create_gecerli_Ok_kod_normalize_upper()
    {
        var result = Country.Create("tr", "Türkiye");

        Assert.True(result.IsSuccess);
        Assert.Equal("TR", result.Data!.Code);
        Assert.Equal("Türkiye", result.Data.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_bos_kod_Error(string code)
    {
        var result = Country.Create(code, "X");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Country.Code) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Fact]
    public void Create_bos_ad_Error()
    {
        var result = Country.Create("TR", "  ");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Country.Name) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }
}

public class CityTests
{
    [Fact]
    public void Create_gecerli_Ok_countryCode_upper()
    {
        var result = City.Create("34", "İstanbul", "tr");

        Assert.True(result.IsSuccess);
        Assert.Equal("34", result.Data!.Code);
        Assert.Equal("İstanbul", result.Data.Name);
        Assert.Equal("TR", result.Data.CountryCode);
    }

    [Fact]
    public void Create_bos_countryCode_Error()
    {
        var result = City.Create("34", "İstanbul", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(City.CountryCode) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Fact]
    public void Create_bos_kod_Error()
    {
        var result = City.Create("", "İstanbul", "TR");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Property == nameof(City.Code));
    }
}

public class MccTests
{
    [Theory]
    [InlineData("5411")]
    [InlineData("0000")]
    public void Create_4_hane_Ok(string code)
    {
        var result = Mcc.Create(code, "X");

        Assert.True(result.IsSuccess);
        Assert.Equal(code, result.Data!.Code);
    }

    [Theory]
    [InlineData("541")]
    [InlineData("54111")]
    [InlineData("abcd")]
    [InlineData("")]
    public void Create_4_hane_degil_Error(string code)
    {
        var result = Mcc.Create(code, "X");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Mcc.Code) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Fact]
    public void Create_bos_ad_Error()
    {
        var result = Mcc.Create("5411", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Property == nameof(Mcc.Name));
    }
}

public class BankTests
{
    [Theory]
    [InlineData("0062", "Garanti BBVA")]
    [InlineData("9999", "Paratika")]
    public void Create_4_hane_Ok(string code, string name)
    {
        var result = Bank.Create(code, name);

        Assert.True(result.IsSuccess);
        Assert.Equal(code, result.Data!.Code);
        Assert.Equal(name, result.Data.Name);
    }

    [Theory]
    [InlineData("062")]
    [InlineData("00622")]
    [InlineData("abcd")]
    [InlineData("")]
    public void Create_4_hane_degil_Error(string code)
    {
        var result = Bank.Create(code, "X");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(Bank.Code) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Fact]
    public void Create_bos_ad_Error()
    {
        var result = Bank.Create("0062", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Property == nameof(Bank.Name));
    }
}