using Merchant.Api.Domains.CredentialRevealLinks;

namespace Merchant.Api.Tests;

// 046 US1 test-first (İLKE VI): reissue teslim linki reuse davranışı — yeni link doğar, eski
// yaşayan linkler Kill sonrası nötr, Consume tek-kullanımlık.
// Senaryo: yeni key yalnız bir kez gösterilir; eski teslim linkleri artık açılmaz.
public class CredentialRevealLinkReissueTests
{
    private static readonly Guid MerchantId = Guid.NewGuid();
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    [Fact]
    public void Kill_YasayanLink_ArtikGosterilemez()
    {
        var now = DateTimeOffset.UtcNow;
        var old = CredentialRevealLink.Create(MerchantId, Lifetime).Data!;
        Assert.True(old.IsUsable(now));

        old.Kill(now);

        // Kill ExpiresAt = now yapar; gerçek gösterim daima sonraki bir anda olur → nötr.
        var later = now + TimeSpan.FromSeconds(1);
        Assert.False(old.IsUsable(later));
        Assert.False(old.Consume(later).IsSuccess);
    }

    [Fact]
    public void Consume_TekKullanimlik_IkinciCagriRet()
    {
        var now = DateTimeOffset.UtcNow;
        var link = CredentialRevealLink.Create(MerchantId, Lifetime).Data!;

        Assert.True(link.Consume(now).IsSuccess);
        Assert.False(link.Consume(now).IsSuccess);
    }

    [Fact]
    public void Kill_TuketilmisLink_ZararsizNoOp()
    {
        var now = DateTimeOffset.UtcNow;
        var link = CredentialRevealLink.Create(MerchantId, Lifetime).Data!;
        link.Consume(now);

        // Tüketilmiş linke Kill dokunmaz (ConsumedAt korunur).
        link.Kill(now);
        Assert.NotNull(link.ConsumedAt);
    }

    [Fact]
    public void Create_ReissueYeniLink_TazeKullanilabilirToken()
    {
        var now = DateTimeOffset.UtcNow;
        var fresh = CredentialRevealLink.Create(MerchantId, Lifetime).Data!;

        Assert.True(fresh.IsUsable(now));
        Assert.Null(fresh.ConsumedAt);
        Assert.False(string.IsNullOrWhiteSpace(fresh.Token));
    }
}