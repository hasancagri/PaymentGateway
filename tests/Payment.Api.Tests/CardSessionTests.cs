namespace Payment.Api.Tests;

// 040 (İlke VI Domain-TDD): CardSession hosted-form korelasyon davranışı + invariant'ları.
public class CardSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private static CardSession Pending()
        => CardSession.Start(Guid.NewGuid(), Guid.NewGuid(), "cf-token-1", Now);

    [Fact]
    public void Start_PendingDogar_AlanlarDogru()
    {
        var conversationId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();

        var s = CardSession.Start(conversationId, merchantId, "cf-token-1", Now);

        Assert.Equal(conversationId, s.Id);
        Assert.Equal(merchantId, s.MerchantId);
        Assert.Equal("cf-token-1", s.CheckoutFormToken);
        Assert.Equal(CardSessionStatus.Pending, s.Status);
        Assert.Null(s.CardUserKey);
        Assert.True(s.IsUsable(Now, Ttl));
    }

    [Fact]
    public void Complete_BosCardUserKey_Reddedilir()
    {
        var s = Pending();

        Assert.False(s.Complete("").IsSuccess);
        Assert.False(s.Complete("  ").IsSuccess);
        Assert.Equal(CardSessionStatus.Pending, s.Status);
        Assert.Null(s.CardUserKey);
    }

    [Fact]
    public void Complete_PendingdenCompleted_CardUserKeyYazilir()
    {
        var s = Pending();

        Assert.True(s.Complete("iyz-user-1").IsSuccess);
        Assert.Equal(CardSessionStatus.Completed, s.Status);
        Assert.Equal("iyz-user-1", s.CardUserKey);
    }

    [Fact]
    public void Complete_IkinciKez_Reddedilir_TekSefer()
    {
        var s = Pending();
        s.Complete("iyz-user-1");

        Assert.False(s.Complete("iyz-user-2").IsSuccess);
        Assert.Equal("iyz-user-1", s.CardUserKey); // değişmez
    }

    [Fact]
    public void Fail_PendingdenFailed_Idempotent()
    {
        var s = Pending();

        Assert.True(s.Fail().IsSuccess);
        Assert.Equal(CardSessionStatus.Failed, s.Status);
        Assert.True(s.Fail().IsSuccess); // idempotent
    }

    [Fact]
    public void Complete_FailedSonrasi_Reddedilir()
    {
        var s = Pending();
        s.Fail();

        Assert.False(s.Complete("iyz-user-1").IsSuccess);
    }

    [Fact]
    public void IsUsable_SureDolunca_False()
    {
        var s = Pending();

        Assert.False(s.IsUsable(Now.Add(Ttl).AddSeconds(1), Ttl));
    }
}
