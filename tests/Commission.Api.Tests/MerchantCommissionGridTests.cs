
namespace Commission.Api.Tests;

public class MerchantCommissionGridTests
{
    [Fact]
    public void CreateDraft_Draft_baslar()
    {
        var grid = MerchantCommissionGrid.CreateDraft(Guid.NewGuid());

        Assert.Equal(GridStatus.Draft, grid.Status);
        Assert.Null(grid.FinalizedAtUtc);
    }

    [Fact]
    public void MarkReady_Ready_ve_FinalizedAt_set()
    {
        var grid = MerchantCommissionGrid.CreateDraft(Guid.NewGuid());

        grid.MarkReady();

        Assert.Equal(GridStatus.Ready, grid.Status);
        Assert.NotNull(grid.FinalizedAtUtc);
    }

    [Fact]
    public void MarkReady_idempotent_FinalizedAt_degismez()
    {
        var grid = MerchantCommissionGrid.CreateDraft(Guid.NewGuid());
        grid.MarkReady();
        var first = grid.FinalizedAtUtc;

        grid.MarkReady();

        Assert.Equal(first, grid.FinalizedAtUtc);
    }

    // Finalize bütünlük kuralı (handler'da kullanılan saf yardımcı):
    [Theory]
    [InlineData(2.0, 3.0, true)]   // rate <= ceiling → below ceiling (geçerli)
    [InlineData(3.0, 3.0, true)]   // eşit → geçerli
    [InlineData(4.0, 3.0, false)]  // tavan aşımı → ihlal
    public void ComputeBelowBankCeiling_dogru(decimal rate, decimal bankMax, bool expected)
    {
        Assert.Equal(expected, GetMerchantCommissions.ComputeBelowBankCeiling(rate, bankMax));
    }

    [Fact]
    public void ComputeBelowBankCeiling_rate_yoksa_false()
    {
        Assert.False(GetMerchantCommissions.ComputeBelowBankCeiling(null, 3.0m));
    }
}
