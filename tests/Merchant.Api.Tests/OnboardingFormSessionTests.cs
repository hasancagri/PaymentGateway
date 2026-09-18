using Merchant.Api.Domains.OnboardingFormSessions;

namespace Merchant.Api.Tests;

// 045 test-first: store-tetikli, süreli + tek başvuruluk hosted form oturumu — saf domain.
public class OnboardingFormSessionTests
{
    private const string Email = "merchant@test.dev";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    [Fact]
    public void Create_Gecerli_OkTokenUretirSureKurar()
    {
        var before = DateTimeOffset.UtcNow;
        var result = OnboardingFormSession.Create(Email, Lifetime);

        Assert.True(result.IsSuccess);
        var session = result.Data!;
        Assert.False(string.IsNullOrWhiteSpace(session.Token));
        Assert.Equal(Email, session.Email);
        Assert.Null(session.ConsumedAt);
        Assert.True(session.ExpiresAt >= before + Lifetime);
        Assert.True(session.ExpiresAt <= DateTimeOffset.UtcNow + Lifetime);
    }

    [Fact]
    public void Create_EmailNormalizeEdilir()
    {
        var session = OnboardingFormSession.Create("  Merchant@Test.DEV ", Lifetime).Data!;

        Assert.Equal("merchant@test.dev", session.Email);
    }

    [Fact]
    public void Create_TokenUrlSafeVeYeterinceUzun()
    {
        var token = OnboardingFormSession.Create(Email, Lifetime).Data!.Token;

        // 256-bit = 32 bayt → base64url 43 karakter; +, / ve = URL'e giremez.
        Assert.True(token.Length >= 43);
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void Create_IkiOturumFarkliToken()
    {
        Assert.NotEqual(
            OnboardingFormSession.Create(Email, Lifetime).Data!.Token,
            OnboardingFormSession.Create(Email, Lifetime).Data!.Token);
    }

    [Fact]
    public void Create_BosEmail_Error()
    {
        Assert.False(OnboardingFormSession.Create("  ", Lifetime).IsSuccess);
    }

    [Fact]
    public void Create_PozitifOlmayanSure_Error()
    {
        Assert.False(OnboardingFormSession.Create(Email, TimeSpan.Zero).IsSuccess);
    }

    [Fact]
    public void Consume_MutluYol_OkVeOturumOlur()
    {
        var session = OnboardingFormSession.Create(Email, Lifetime).Data!;
        var now = DateTimeOffset.UtcNow;

        Assert.True(session.Consume(now).IsSuccess);
        Assert.Equal(now, session.ConsumedAt);
        Assert.False(session.IsUsable(now));
    }

    [Fact]
    public void Consume_IkinciKez_ErrorIlkAnKorunur()
    {
        var session = OnboardingFormSession.Create(Email, Lifetime).Data!;
        var now = DateTimeOffset.UtcNow;
        Assert.True(session.Consume(now).IsSuccess);

        Assert.False(session.Consume(now.AddSeconds(1)).IsSuccess);
        Assert.Equal(now, session.ConsumedAt);
    }

    [Fact]
    public void Consume_SuresiGecmis_Error()
    {
        var session = OnboardingFormSession.Create(Email, Lifetime).Data!;

        Assert.False(session.Consume(session.ExpiresAt.AddSeconds(1)).IsSuccess);
        Assert.Null(session.ConsumedAt);
    }

    [Fact]
    public void IsUsable_TazeTrueSuresiGecmisFalse()
    {
        var session = OnboardingFormSession.Create(Email, Lifetime).Data!;

        Assert.True(session.IsUsable(DateTimeOffset.UtcNow));
        Assert.False(session.IsUsable(session.ExpiresAt.AddSeconds(1)));
    }
}
