namespace Payment.Api.Tests;

// 031: StoredCard.Revoke — soft + idempotent.
public class StoredCardRevokeTests
{
    private sealed class PassThroughProtector : IPanProtector
    {
        public string Protect(string pan) => pan;
    }

    private static StoredCard NewCard() =>
        StoredCard.Create(Guid.NewGuid(), "4111111111111111", "12/34", "CARD HOLDER",
            new PassThroughProtector()).Data!;

    [Fact]
    public void Revoke_ActiveKart_RevokedOlur()
    {
        var card = NewCard();

        var result = card.Revoke();

        Assert.True(result.IsSuccess);
        Assert.Equal(StoredCardStatus.Revoked, card.Status);
    }

    [Fact]
    public void Revoke_ZatenRevoked_Idempotent()
    {
        var card = NewCard();
        card.Revoke();

        var result = card.Revoke();

        Assert.True(result.IsSuccess);
        Assert.Equal(StoredCardStatus.Revoked, card.Status);
    }
}
