namespace Payment.Api.Tests;

// 032 (Model A): StoredCard.Create artık sağlayıcı (iyzico) kimliklerini alır — Luhn/expiry/AES YOK
// (iyzico handler'da doğrular). Bu testler yalnız aggregate sarma invariant'larını kapsar.
public class StoredCardCreateTests
{
    private static ResultDomain<StoredCard> Create(
        string cardUserKey = "iyz-user-1",
        string cardToken = "iyz-card-1",
        string bin = "552879",
        string last4 = "0008",
        CardBrand brand = CardBrand.MasterCard,
        string expiry = "12/30",
        string holder = "CARD HOLDER",
        Guid? merchantId = null)
        => StoredCard.Create(merchantId ?? Guid.NewGuid(), cardUserKey, cardToken, bin, last4, brand, expiry, holder);

    [Fact]
    public void Create_GecerliKimlikler_TokenVeAlanlarDogru()
    {
        var result = Create();

        Assert.True(result.IsSuccess);
        var card = result.Data!;
        Assert.StartsWith("card_", card.Token);
        Assert.Equal(StoredCardStatus.Active, card.Status);
        Assert.Equal("iyz-user-1", card.CardUserKey);
        Assert.Equal("iyz-card-1", card.CardToken);
        Assert.Equal("552879", card.Bin);
        Assert.Equal("0008", card.Last4);
        Assert.Equal(CardBrand.MasterCard, card.Brand);
    }

    [Fact]
    public void Create_BosCardUserKey_Reddedilir()
    {
        var result = Create(cardUserKey: "  ");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_BosCardToken_Reddedilir()
    {
        var result = Create(cardToken: "");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_BosMerchantId_Reddedilir()
    {
        var result = Create(merchantId: Guid.Empty);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_HerCagri_FarkliOpakToken()
    {
        var first = Create().Data!;
        var second = Create().Data!;

        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public void Create_PanAlaniYok_YalnizSaglayiciKimlikleri()
    {
        // Model A kanıtı: aggregate'te ham/şifreli PAN alanı yok; yalnız sağlayıcı kimlikleri + gösterim.
        var card = Create().Data!;

        Assert.Null(typeof(StoredCard).GetProperty("EncryptedPan"));
        Assert.NotNull(typeof(StoredCard).GetProperty("CardUserKey"));
        Assert.NotNull(typeof(StoredCard).GetProperty("CardToken"));
    }
}
