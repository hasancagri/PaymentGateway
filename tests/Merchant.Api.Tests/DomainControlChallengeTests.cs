using Merchant.Api.Domains.DomainControlChallenges;
using Xunit;

namespace Merchant.Api.Tests;

public class DomainControlChallengeTests
{
    [Fact]
    public void Issue_Issued_durumda_token_ve_deger_uretir()
    {
        var c = DomainControlChallenge.Issue("shop.example.com").Data!;

        Assert.Equal(ChallengeStatus.Issued, c.Status);
        Assert.False(string.IsNullOrWhiteSpace(c.Token));
        Assert.False(string.IsNullOrWhiteSpace(c.ExpectedValue));
        Assert.Equal("shop.example.com", c.Domain);
    }

    [Fact]
    public void Verify_dogru_deger_Passed_ve_Consumed()
    {
        var c = DomainControlChallenge.Issue("shop.example.com").Data!;

        var outcome = c.Verify(c.ExpectedValue, DateTime.UtcNow).Data!;

        Assert.Equal(ChallengeOutcome.Passed, outcome);
        Assert.Equal(ChallengeStatus.Consumed, c.Status);
    }

    [Fact]
    public void Verify_yanlis_deger_Failed_ve_Issued_kalir()
    {
        var c = DomainControlChallenge.Issue("shop.example.com").Data!;

        var outcome = c.Verify("yanlis", DateTime.UtcNow).Data!;

        Assert.Equal(ChallengeOutcome.Failed, outcome);
        Assert.Equal(ChallengeStatus.Issued, c.Status);
    }

    [Fact]
    public void Verify_null_deger_Failed()
    {
        var c = DomainControlChallenge.Issue("shop.example.com").Data!;

        Assert.Equal(ChallengeOutcome.Failed, c.Verify(null, DateTime.UtcNow).Data!);
    }

    [Fact]
    public void Verify_sure_dolmus_Expired()
    {
        var c = DomainControlChallenge.Issue("shop.example.com").Data!;

        var outcome = c.Verify(c.ExpectedValue, DateTime.UtcNow.AddHours(DomainControlChallenge.TtlHours + 1)).Data!;

        Assert.Equal(ChallengeOutcome.Expired, outcome);
        Assert.Equal(ChallengeStatus.Expired, c.Status);
    }

    [Fact]
    public void Verify_Consumed_sonrasi_tekrar_dogrulanamaz_ama_Passed_doner()
    {
        var c = DomainControlChallenge.Issue("shop.example.com").Data!;
        c.Verify(c.ExpectedValue, DateTime.UtcNow);

        // Tek-kullanım: tekrar Verify durum değiştirmez, Passed sonucu sabit kalır.
        var again = c.Verify("baska", DateTime.UtcNow).Data!;

        Assert.Equal(ChallengeOutcome.Passed, again);
        Assert.Equal(ChallengeStatus.Consumed, c.Status);
    }
}
