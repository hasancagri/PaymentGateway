
namespace Payment.Api.Tests;

public class ImportBinCardsTests
{
    private static ImportBinCards.BinCardImportItem Item(string? bin, string? bank) =>
        new(bin, bank, CardType: 1, CardBrand: 0, Commercial: false, CardProgram: 3);

    [Fact]
    public void IsValid_binNumber_ve_bankCode_dolu_ise_gecerli()
    {
        Assert.True(ImportBinCards.IsValid(Item("365770", "0124")));
    }

    [Theory]
    [InlineData(null, "0124")]
    [InlineData("", "0124")]
    [InlineData("  ", "0124")]
    [InlineData("365770", null)]
    [InlineData("365770", "")]
    public void IsValid_binNumber_veya_bankCode_eksik_ise_gecersiz(string? bin, string? bank)
    {
        Assert.False(ImportBinCards.IsValid(Item(bin, bank)));
    }

    [Fact]
    public void FromCodes_import_kaydini_dogru_esler()
    {
        var item = new ImportBinCards.BinCardImportItem("374422", "0062", CardType: 1, CardBrand: 3, Commercial: false, CardProgram: 6);

        var card = BinCardMapping.FromCodes(
            item.BinNumber!, item.BankCode, item.CardType, item.CardBrand, item.CardProgram, item.Commercial);

        Assert.Equal("374422", card.BinNumber);
        Assert.Equal("0062", card.BankCode);
        Assert.Equal(CardType.Credit, card.CardType);
        Assert.Equal(CardBrand.Amex, card.CardBrand);
        Assert.Equal(CardProgram.MilesAndSmiles, card.CardProgram);
        Assert.False(card.Commercial);
    }
}