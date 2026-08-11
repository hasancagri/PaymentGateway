namespace Payment.Api.Tests;

public class StoredCardCreateTests
{
    // Test IPanProtector — reversible olmayan, ham PAN'ı taşımayan sabit dönüşüm (aggregate testi saf).
    private sealed class FakePanProtector : IPanProtector
    {
        public string Protect(string pan) => "enc::" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pan));
    }

    private const string ValidVisa = "4111111111111111"; // Luhn geçer
    private static readonly Guid Merchant = Guid.NewGuid();
    private static string FutureExpiry() => DateTime.UtcNow.AddYears(2).ToString("MM/yy", System.Globalization.CultureInfo.InvariantCulture);

    private static ResultDomain<StoredCard> CreateValid() =>
        StoredCard.Create(Merchant, ValidVisa, FutureExpiry(), "AHMET YILMAZ", new FakePanProtector());

    [Fact]
    public void Create_gecerli_Ok_ve_Active_baslar()
    {
        var result = CreateValid();

        Assert.True(result.IsSuccess);
        Assert.Equal(StoredCardStatus.Active, result.Data!.Status);
        Assert.StartsWith("card_", result.Data.Token);
    }

    [Fact]
    public void Create_turetilmis_bin_last4_brand_dogru()
    {
        var card = CreateValid().Data!;

        Assert.Equal("411111", card.Bin);
        Assert.Equal("1111", card.Last4);
        Assert.Equal(CardBrand.Visa, card.Brand);
    }

    [Fact]
    public void Create_EncryptedPan_ham_PAN_degil()
    {
        var card = CreateValid().Data!;

        Assert.DoesNotContain(ValidVisa, card.EncryptedPan);
        Assert.NotEqual(ValidVisa, card.EncryptedPan);
    }

    [Fact]
    public void Create_Luhn_gecmeyen_PAN_Error()
    {
        var result = StoredCard.Create(Merchant, "4111111111111112", FutureExpiry(), "X", new FakePanProtector());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_gecmis_expiry_Error()
    {
        var past = DateTime.UtcNow.AddYears(-1).ToString("MM/yy", System.Globalization.CultureInfo.InvariantCulture);
        var result = StoredCard.Create(Merchant, ValidVisa, past, "X", new FakePanProtector());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_bos_holder_Error()
    {
        var result = StoredCard.Create(Merchant, ValidVisa, FutureExpiry(), "  ", new FakePanProtector());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_ayni_PAN_iki_kez_farkli_Token_non_idempotent()
    {
        var a = CreateValid().Data!;
        var b = CreateValid().Data!;

        Assert.NotEqual(a.Token, b.Token);
    }

    [Theory]
    [InlineData("4111111111111111", CardBrand.Visa)]
    [InlineData("5555555555554444", CardBrand.MasterCard)]
    [InlineData("2223000048410010", CardBrand.MasterCard)]
    [InlineData("378282246310005", CardBrand.Amex)]
    public void BrandDetector_prefixten_dogru_marka(string pan, CardBrand expected)
    {
        Assert.Equal(expected, BrandDetector.Detect(pan));
    }
}
