using Payment.Api.Domains.StoredCards.ValueObjects;

namespace Payment.Api.Tests;

// 035: CardInformation VO saf doğrulama testleri (expiry + boş; Luhn YOK — Model A).
public class CardInformationValueObjectTests
{
    [Fact]
    public void Create_GecerliKart_Ok()
    {
        var r = CardInformation.Create("5406 6700 0000 0009", "12/30", "Ada Lovelace");
        Assert.True(r.IsSuccess);
        Assert.Equal("5406670000000009", r.Data!.CardNumber);
        Assert.Equal("12", r.Data!.ExpireMonth);
        Assert.Equal("2030", r.Data!.ExpireYear);
        Assert.Equal("12/30", r.Data!.RawExpiry);
    }

    [Theory]
    [InlineData("1230")]
    [InlineData("12/2030")]
    [InlineData("")]
    public void Create_GecersizExpiry_Error(string expiry)
    {
        var r = CardInformation.Create("5406670000000009", expiry, "Ada Lovelace");
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void Create_BosPan_Error()
    {
        var r = CardInformation.Create("abc", "12/30", "Ada Lovelace");
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void Create_BosSahip_Error()
    {
        var r = CardInformation.Create("5406670000000009", "12/30", "  ");
        Assert.False(r.IsSuccess);
    }
}
