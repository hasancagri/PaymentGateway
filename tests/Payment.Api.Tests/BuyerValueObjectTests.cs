using Payment.Api.Domains.Payments.ValueObjects;

namespace Payment.Api.Tests;

// 035: Buyer VO saf doğrulama testleri (yapısal — boş + e-posta + kimlik).
public class BuyerValueObjectTests
{
    private static ResultDomain<Buyer> Valid() => Buyer.Create(
        "Ada", "Lovelace", "ada@dropshop.com", "5551112233", "10000000146",
        "Analitik Cd. 1", "Istanbul", "Turkey", "85.1.2.3");

    [Fact]
    public void Create_GecerliAlanlar_Ok()
    {
        var r = Valid();
        Assert.True(r.IsSuccess);
        Assert.Equal("Ada", r.Data!.Name);
        Assert.Equal("10000000146", r.Data!.IdentityNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ada-at-dropshop.com")]
    [InlineData("ada@dropshop")]
    public void Create_GecersizEmail_Error(string email)
    {
        var r = Buyer.Create("Ada", "Lovelace", email, "5551112233", "10000000146",
            "Analitik Cd. 1", "Istanbul", "Turkey", "85.1.2.3");
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void Create_KimlikOnBirHaneDegil_Error()
    {
        var r = Buyer.Create("Ada", "Lovelace", "ada@dropshop.com", "5551112233", "123",
            "Analitik Cd. 1", "Istanbul", "Turkey", "85.1.2.3");
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void Create_BosZorunluAlan_Error()
    {
        var r = Buyer.Create("", "Lovelace", "ada@dropshop.com", "5551112233", "10000000146",
            "Analitik Cd. 1", "Istanbul", "Turkey", "85.1.2.3");
        Assert.False(r.IsSuccess);
    }
}
