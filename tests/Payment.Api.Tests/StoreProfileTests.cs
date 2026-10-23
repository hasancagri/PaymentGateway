using Payment.Api.Constants;
using Payment.Api.Domains.StoreOnboarding.ValueObjects;

namespace Payment.Api.Tests;

// 091 (İlke VIII Domain-TDD): StoreProfile çekirdek VO doğrulaması — zorunlu alan + TR IBAN mod-97 + MCC 4 hane.
public class StoreProfileTests
{
    // Wikipedia TR IBAN örneği (mod-97 geçerli).
    private const string ValidIban = "TR330006100519786457841326";

    [Fact]
    public void Create_GecerliProfil_Ok()
    {
        var r = StoreProfile.Create("Acme A.Ş.", "1234567890", ValidIban, "5411", "https://acme.example", "Ada Lovelace", "ada@acme.example");
        Assert.True(r.IsSuccess);
        Assert.Equal(ValidIban, r.Data!.Iban);
    }

    [Fact]
    public void Create_BosAlan_InvalidProfile()
    {
        var r = StoreProfile.Create("", "1234567890", ValidIban, "5411", "https://acme.example", "Ada", "ada@acme.example");
        Assert.False(r.IsSuccess);
        Assert.Equal(StoreOnboardingResourceConstants.INVALID_PROFILE, r.Messages!.First().Code);
    }

    [Fact]
    public void Create_GecersizIban_InvalidProfile()
    {
        var r = StoreProfile.Create("Acme", "1234567890", "TR000000000000000000000000", "5411", "https://acme.example", "Ada", "ada@acme.example");
        Assert.False(r.IsSuccess);
        Assert.Equal(StoreOnboardingResourceConstants.INVALID_PROFILE, r.Messages!.First().Code);
    }

    [Fact]
    public void Create_GecersizMcc_InvalidProfile()
    {
        var r = StoreProfile.Create("Acme", "1234567890", ValidIban, "54", "https://acme.example", "Ada", "ada@acme.example");
        Assert.False(r.IsSuccess);
        Assert.Equal(StoreOnboardingResourceConstants.INVALID_PROFILE, r.Messages!.First().Code);
    }
}
