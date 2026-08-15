using Payment.Api.Domains.Payments.ValueObjects;

namespace Payment.Api.Tests;

// 035: BasketItem VO saf doğrulama testleri.
public class BasketItemValueObjectTests
{
    [Fact]
    public void Create_GecerliAlanlar_Ok()
    {
        var r = BasketItem.Create("SKU-1", "Kalem", "Kirtasiye", 12.5m);
        Assert.True(r.IsSuccess);
        Assert.Equal(12.5m, r.Data!.Price);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_FiyatSifirVeyaNegatif_Error(int price)
    {
        var r = BasketItem.Create("SKU-1", "Kalem", "Kirtasiye", price);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void Create_BosAlan_Error()
    {
        var r = BasketItem.Create("", "Kalem", "Kirtasiye", 10m);
        Assert.False(r.IsSuccess);
    }
}
