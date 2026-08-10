namespace Payment.Api.Tests;

public class StoredCardRevokeTests
{
    private sealed class FakePanProtector : IPanProtector
    {
        public string Protect(string pan) => "enc::" + pan.GetHashCode();
    }

    private static StoredCard Active() =>
        StoredCard.Create(Guid.NewGuid(), "4111111111111111",
            DateTime.UtcNow.AddYears(2).ToString("MM/yy", System.Globalization.CultureInfo.InvariantCulture), "AHMET YILMAZ", new FakePanProtector()).Data!;

    [Fact]
    public void Revoke_Active_kart_Revoked_olur()
    {
        var card = Active();

        var result = card.Revoke();

        Assert.True(result.IsSuccess);
        Assert.Equal(StoredCardStatus.Revoked, card.Status);
    }

    [Fact]
    public void Revoke_idempotent_zaten_Revoked_Ok()
    {
        var card = Active();
        card.Revoke();

        var again = card.Revoke();

        Assert.True(again.IsSuccess);
        Assert.Equal(StoredCardStatus.Revoked, card.Status);
    }

    [Fact]
    public void Revoked_kart_UpdateDetails_RET()
    {
        var card = Active();
        card.Revoke();

        var result = card.UpdateDetails(DateTime.UtcNow.AddYears(3).ToString("MM/yy", System.Globalization.CultureInfo.InvariantCulture), "YENI AD");

        Assert.False(result.IsSuccess);
        Assert.Equal(StoredCardStatus.Revoked, card.Status);
    }
}
