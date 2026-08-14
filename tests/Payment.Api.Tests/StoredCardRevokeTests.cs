namespace Payment.Api.Tests;

// 032: StoredCard.Revoke — soft + idempotent (davranış 031'den değişmedi; Create imzası değişti).
public class StoredCardRevokeTests
{
    private static StoredCard NewCard() =>
        StoredCard.Create(Guid.NewGuid(), "iyz-user-1", "iyz-card-1", "552879", "0008",
            CardBrand.MasterCard, "12/30", "CARD HOLDER").Data!;

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
