namespace Payment.Api.Tests;

// 031: StoredCard.Create davranış testleri — saf domain (DB/HTTP yok). Protector no-op sarmalayıcıyla
// test edilir (koruma çıktısı test kapsamı değil; PAN'ın ham dönmediği DB'de doğrulanır — quickstart).
// Geçerli Visa test PAN'ı: 4111111111111111 (Luhn geçerli).
public class StoredCardCreateTests
{
    private sealed class PassThroughProtector : IPanProtector
    {
        public string Protect(string pan) => "enc(" + pan + ")";
    }

    private static readonly IPanProtector Protector = new PassThroughProtector();

    private static ResultDomain<StoredCard> Create(
        string pan = "4111111111111111",
        string expiry = "12/34",
        string holder = "CARD HOLDER",
        Guid? merchantId = null)
        => StoredCard.Create(merchantId ?? Guid.NewGuid(), pan, expiry, holder, Protector);

    [Fact]
    public void Create_GecerliKart_TokenVeTuretimlerDogru()
    {
        var result = Create();

        Assert.True(result.IsSuccess);
        var card = result.Data!;
        Assert.StartsWith("card_", card.Token);
        Assert.Equal(StoredCardStatus.Active, card.Status);
        Assert.Equal("411111", card.Bin);
        Assert.Equal("1111", card.Last4);
        Assert.Equal(CardBrand.Visa, card.Brand);
    }

    [Fact]
    public void Create_MastercardPan_MarkaMasterCard()
    {
        // 5555555555554444 — Luhn geçerli Mastercard test PAN'ı.
        var result = Create(pan: "5555555555554444");

        Assert.True(result.IsSuccess);
        Assert.Equal(CardBrand.MasterCard, result.Data!.Brand);
    }

    [Fact]
    public void Create_BoslukluTireliPan_NormalizeEdilipGecer()
    {
        var result = Create(pan: "4111-1111 1111-1111");

        Assert.True(result.IsSuccess);
        Assert.Equal("411111", result.Data!.Bin);
        Assert.Equal("1111", result.Data!.Last4);
    }

    [Fact]
    public void Create_LuhnGecersiz_Reddedilir()
    {
        var result = Create(pan: "4111111111111112");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Fact]
    public void Create_On2HanedenKisa_Reddedilir()
    {
        var result = Create(pan: "41111111111"); // 11 hane

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_On9HanedenUzun_Reddedilir()
    {
        var result = Create(pan: "41111111111111111111"); // 20 hane

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_GecmisExpiry_Reddedilir()
    {
        var result = Create(expiry: "01/20");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, m => m.Property == "expiry");
    }

    [Fact]
    public void Create_BozukExpiryBicimi_Reddedilir()
    {
        var result = Create(expiry: "2029-12");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_BosHolder_Reddedilir()
    {
        var result = Create(holder: "  ");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_BosMerchantId_Reddedilir()
    {
        var result = Create(merchantId: Guid.Empty);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_AyniPan_HerCagridaFarkliToken()
    {
        var first = Create().Data!;
        var second = Create().Data!;

        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public void Create_GercekKoruyucu_HamPanEncOutputtaGorunmez()
    {
        // Gerçek DevPanProtector (AES) ile: korunmuş çıktı ham PAN'ı içermez (SC-002 zemini).
        var result = StoredCard.Create(Guid.NewGuid(), "4111111111111111", "12/34", "CARD HOLDER",
            new DevPanProtector());

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("4111111111111111", result.Data!.EncryptedPan);
    }
}
