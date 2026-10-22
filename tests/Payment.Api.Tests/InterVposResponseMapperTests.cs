namespace Payment.Api.Tests;

// Phase-1 (İlke VII/VIII Domain-TDD): InterVpos `;;` cevabı → normalize BankChargeOutcome anti-corruption.
public class InterVposResponseMapperTests
{
    [Fact]
    public void Map_Onay_Approved()
    {
        var o = InterVposResponseMapper.Map(
            "ProcReturnCode=00;;Response=Approved;;OrderId=ORD-1;;TransId=TID9;;AuthCode=P00ABC;;TRXDATE=2026-10-21 10:00:00");
        Assert.True(o.IsApproved);
        Assert.Equal("TID9", o.ProviderRef);
        Assert.Equal("P00ABC", o.AuthCode);
        Assert.Equal("00", o.RawCode);
    }

    [Theory]
    [InlineData("51", DeclineClass.IssuerDecline)]
    [InlineData("05", DeclineClass.IssuerDecline)]
    [InlineData("59", DeclineClass.FraudDecline)]
    [InlineData("91", DeclineClass.TechnicalFailure)]
    [InlineData("99", DeclineClass.ConfigError)]
    [InlineData("77", DeclineClass.IssuerDecline)] // bilinmeyen → en güvenli issuer
    public void Map_RedKodu_DeclineClassEslemesi(string code, DeclineClass expected)
    {
        var o = InterVposResponseMapper.Map($"ProcReturnCode={code};;Response=Declined;;OrderId=ORD-1;;ErrorCode={code}");
        Assert.False(o.IsApproved);
        Assert.Equal(expected, o.DeclineClass);
        Assert.Equal(code, o.RawCode);
    }

    [Fact]
    public void Map_BosGovde_BilinmeyenRed()
    {
        var o = InterVposResponseMapper.Map("");
        Assert.False(o.IsApproved);
        Assert.Equal("UNKNOWN", o.RawCode);
        Assert.Equal(DeclineClass.IssuerDecline, o.DeclineClass);
    }

    // --- In-doubt: MapStatus (çift-çekim emniyeti) ---

    [Theory]
    [InlineData("Authorized")]
    [InlineData("Settled")]
    public void MapStatus_BankadaVar_OnayKurtarilir(string txnStat)
    {
        var o = InterVposResponseMapper.MapStatus(
            $"ProcReturnCode=00;;OrderId=ORD-1;;TxnStat={txnStat};;VoidDate=;;RefundedAmount=0.00");
        Assert.True(o.IsApproved); // banka çekmiş → onay kurtarıldı (çift çekim önlendi)
    }

    [Fact]
    public void MapStatus_Bulunamadi_TeknikBelirsiz()
    {
        var o = InterVposResponseMapper.MapStatus(
            "ProcReturnCode=99;;Response=Error;;ErrorCode=NOTFOUND;;ErrorMessage=Islem bulunamadi");
        Assert.False(o.IsApproved);
        Assert.Equal(DeclineClass.TechnicalFailure, o.DeclineClass); // çekilmemiş say, retry güvenli
    }

    [Fact]
    public void MapStatus_Voided_TeknikBelirsiz()
    {
        var o = InterVposResponseMapper.MapStatus("ProcReturnCode=00;;OrderId=ORD-1;;TxnStat=Voided;;RefundedAmount=0.00");
        Assert.False(o.IsApproved); // iptal edilmiş → onay değil
    }
}
