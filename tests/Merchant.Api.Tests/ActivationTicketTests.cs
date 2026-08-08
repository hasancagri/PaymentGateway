using Merchant.Api.Domains.ActivationTickets;
using Xunit;

namespace Merchant.Api.Tests;

public class ActivationTicketTests
{
    [Fact]
    public void Issue_Issued_baslar()
    {
        var t = ActivationTicket.Issue(Guid.NewGuid()).Data!;

        Assert.Equal(ActivationTicketStatus.Issued, t.Status);
        Assert.False(string.IsNullOrWhiteSpace(t.Token));
    }

    [Fact]
    public void Redeem_ilk_kez_Ok_ve_Redeemed()
    {
        var t = ActivationTicket.Issue(Guid.NewGuid()).Data!;

        var r = t.Redeem(DateTime.UtcNow);

        Assert.True(r.IsSuccess);
        Assert.Equal(ActivationTicketStatus.Redeemed, t.Status);
        Assert.NotNull(t.RedeemedAtUtc);
    }

    [Fact]
    public void Redeem_ikinci_kez_RET()
    {
        var t = ActivationTicket.Issue(Guid.NewGuid()).Data!;
        t.Redeem(DateTime.UtcNow);

        var second = t.Redeem(DateTime.UtcNow);

        Assert.False(second.IsSuccess);
        Assert.Equal(ActivationTicketStatus.Redeemed, t.Status);
    }

    [Fact]
    public void Redeem_sure_dolmus_RET_ve_Expired()
    {
        var t = ActivationTicket.Issue(Guid.NewGuid()).Data!;

        var r = t.Redeem(DateTime.UtcNow.AddHours(ActivationTicket.TtlHours + 1));

        Assert.False(r.IsSuccess);
        Assert.Equal(ActivationTicketStatus.Expired, t.Status);
    }
}
