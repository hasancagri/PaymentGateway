namespace Payment.Api.Tests;

public class StoredCardUpdateTests
{
    private sealed class FakePanProtector : IPanProtector
    {
        public string Protect(string pan) => "enc::" + pan.Length;
    }

    private static StoredCard Active() =>
        StoredCard.Create(Guid.NewGuid(), "4111111111111111",
            DateTime.UtcNow.AddYears(2).ToString("MM/yy", System.Globalization.CultureInfo.InvariantCulture), "AHMET YILMAZ", new FakePanProtector()).Data!;

    [Fact]
    public void UpdateDetails_yalnizca_expiry_holder_degisir_PAN_token_immutable()
    {
        var card = Active();
        var token = card.Token;
        var bin = card.Bin;
        var last4 = card.Last4;
        var brand = card.Brand;
        var enc = card.EncryptedPan;
        var newExpiry = DateTime.UtcNow.AddYears(3).ToString("MM/yy", System.Globalization.CultureInfo.InvariantCulture);

        var result = card.UpdateDetails(newExpiry, "MEHMET DEMIR");

        Assert.True(result.IsSuccess);
        Assert.Equal(newExpiry, card.Expiry);
        Assert.Equal("MEHMET DEMIR", card.HolderName);
        Assert.Equal(token, card.Token);
        Assert.Equal(bin, card.Bin);
        Assert.Equal(last4, card.Last4);
        Assert.Equal(brand, card.Brand);
        Assert.Equal(enc, card.EncryptedPan);
    }

    [Fact]
    public void UpdateDetails_gecmis_expiry_RET()
    {
        var card = Active();

        var result = card.UpdateDetails(DateTime.UtcNow.AddYears(-1).ToString("MM/yy", System.Globalization.CultureInfo.InvariantCulture), "X");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void UpdateDetails_bos_holder_RET()
    {
        var card = Active();

        var result = card.UpdateDetails(DateTime.UtcNow.AddYears(2).ToString("MM/yy", System.Globalization.CultureInfo.InvariantCulture), "  ");

        Assert.False(result.IsSuccess);
    }
}
