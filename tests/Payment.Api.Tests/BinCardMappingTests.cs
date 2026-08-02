namespace Payment.Api.Tests;

public class BinCardMappingTests
{
    [Theory]
    [InlineData(1, CardType.Credit)]
    [InlineData(0, CardType.Debit)]
    [InlineData(7, CardType.Debit)] // tanınmayan → Debit (yalnız 1 = Credit)
    public void MapCardType_beklenen_degeri_doner(int value, CardType expected) =>
        Assert.Equal(expected, BinCardMapping.MapCardType(value));

    [Theory]
    [InlineData(-1, CardBrand.Unknown)]
    [InlineData(0, CardBrand.Visa)]
    [InlineData(1, CardBrand.MasterCard)]
    [InlineData(2, CardBrand.Troy)]
    [InlineData(3, CardBrand.Amex)]
    [InlineData(4, CardBrand.Discover)]
    [InlineData(5, CardBrand.Unionpay)]
    [InlineData(6, CardBrand.JCB)]
    [InlineData(99, CardBrand.Unknown)] // tanınmayan → Unknown
    public void MapCardBrand_tum_degerler_ve_taninmayan_Unknown(int value, CardBrand expected) =>
        Assert.Equal(expected, BinCardMapping.MapCardBrand(value));

    [Theory]
    [InlineData(-1, CardProgram.Unknown)]
    [InlineData(0, CardProgram.Axess)]
    [InlineData(3, CardProgram.Bonus)]
    [InlineData(9, CardProgram.ShopAndFly)]
    [InlineData(11, CardProgram.World)]
    [InlineData(13, CardProgram.SaglamKart)]
    [InlineData(99, CardProgram.Unknown)] // tanınmayan → Unknown
    public void MapCardProgram_degerler_ve_taninmayan_Unknown(int value, CardProgram expected) =>
        Assert.Equal(expected, BinCardMapping.MapCardProgram(value));

    [Fact]
    public void FromCodes_tum_alanlari_dogru_esler()
    {
        var card = BinCardMapping.FromCodes("365770", "0124", cardType: 1, cardBrand: 2, cardProgram: 3, commercial: true);

        Assert.Equal("365770", card.BinNumber);
        Assert.Equal("0124", card.BankCode);
        Assert.Equal(CardType.Credit, card.CardType);
        Assert.Equal(CardBrand.Troy, card.CardBrand);
        Assert.Equal(CardProgram.Bonus, card.CardProgram);
        Assert.True(card.Commercial);
    }

    [Fact]
    public void FromCodes_null_bankCode_bos_stringe_dusurulur()
    {
        var card = BinCardMapping.FromCodes("365770", null, 1, 0, 0, false);

        Assert.Equal(string.Empty, card.BankCode);
    }
}