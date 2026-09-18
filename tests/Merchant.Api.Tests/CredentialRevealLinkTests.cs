using Merchant.Api.Domains.CredentialRevealLinks;

namespace Merchant.Api.Tests;

// 045 test-first: approve/regenerate ürünü tek gösterimlik teslim linki — saf domain.
// GET tüketir (gösterim = teslim); Kill regenerate'te eski linki öldürür.
public class CredentialRevealLinkTests
{
    private static readonly Guid MerchantId = Guid.NewGuid();
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    [Fact]
    public void Create_Gecerli_OkTokenUretirSureKurar()
    {
        var before = DateTimeOffset.UtcNow;
        var result = CredentialRevealLink.Create(MerchantId, Lifetime);

        Assert.True(result.IsSuccess);
        var link = result.Data!;
        Assert.False(string.IsNullOrWhiteSpace(link.Token));
        Assert.Equal(MerchantId, link.MerchantId);
        Assert.Null(link.ConsumedAt);
        Assert.True(link.ExpiresAt >= before + Lifetime);
        Assert.True(link.ExpiresAt <= DateTimeOffset.UtcNow + Lifetime);
    }

    [Fact]
    public void Create_TokenUrlSafeVeYeterinceUzun()
    {
        var token = CredentialRevealLink.Create(MerchantId, Lifetime).Data!.Token;

        Assert.True(token.Length >= 43);
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void Create_IkiLinkFarkliToken()
    {
        Assert.NotEqual(
            CredentialRevealLink.Create(MerchantId, Lifetime).Data!.Token,
            CredentialRevealLink.Create(MerchantId, Lifetime).Data!.Token);
    }

    [Fact]
    public void Create_BosMerchant_Error()
    {
        Assert.False(CredentialRevealLink.Create(Guid.Empty, Lifetime).IsSuccess);
    }

    [Fact]
    public void Create_PozitifOlmayanSure_Error()
    {
        Assert.False(CredentialRevealLink.Create(MerchantId, TimeSpan.Zero).IsSuccess);
    }

    [Fact]
    public void Consume_MutluYol_OkIkinciGosterimYok()
    {
        var link = CredentialRevealLink.Create(MerchantId, Lifetime).Data!;
        var now = DateTimeOffset.UtcNow;

        Assert.True(link.Consume(now).IsSuccess);
        Assert.Equal(now, link.ConsumedAt);
        Assert.False(link.IsUsable(now));
        Assert.False(link.Consume(now.AddSeconds(1)).IsSuccess);
    }

    [Fact]
    public void Consume_SuresiGecmis_Error()
    {
        var link = CredentialRevealLink.Create(MerchantId, Lifetime).Data!;

        Assert.False(link.Consume(link.ExpiresAt.AddSeconds(1)).IsSuccess);
        Assert.Null(link.ConsumedAt);
    }

    [Fact]
    public void Kill_YasayanLinkiOldurur()
    {
        var link = CredentialRevealLink.Create(MerchantId, Lifetime).Data!;
        var now = DateTimeOffset.UtcNow;

        link.Kill(now);

        Assert.False(link.IsUsable(now.AddSeconds(1)));
        Assert.False(link.Consume(now.AddSeconds(1)).IsSuccess);
    }

    [Fact]
    public void Kill_TuketilmisLinkteZararsiz()
    {
        var link = CredentialRevealLink.Create(MerchantId, Lifetime).Data!;
        var now = DateTimeOffset.UtcNow;
        Assert.True(link.Consume(now).IsSuccess);

        link.Kill(now.AddSeconds(1));

        Assert.Equal(now, link.ConsumedAt); // ilk gösterim izi korunur
    }
}
