namespace Payment.Api.Tests;

// Phase-1 (İlke VII/VIII): TuruncuBank XML cevabı → normalize BankChargeOutcome anti-corruption (SHA512/XML
// farklı tel, aynı port). InterVpos `;;` mapper'ının XML muadili.
public class TuruncuResponseParserTests
{
    [Fact]
    public void Parse_Onay_Approved()
    {
        var o = TuruncuResponseParser.Parse(
            "<PaymentResponse><ResultCode>00</ResultCode><OrderId>ORD-1</OrderId><TransId>TID9</TransId><AuthCode>P00ABC</AuthCode></PaymentResponse>");
        Assert.True(o.IsApproved);
        Assert.Equal("TID9", o.ProviderRef);
        Assert.Equal("P00ABC", o.AuthCode);
    }

    [Theory]
    [InlineData("51", DeclineClass.IssuerDecline)]
    [InlineData("59", DeclineClass.FraudDecline)]
    [InlineData("91", DeclineClass.TechnicalFailure)]
    [InlineData("99", DeclineClass.ConfigError)]
    public void Parse_RedKodu_DeclineClass(string code, DeclineClass expected)
    {
        var o = TuruncuResponseParser.Parse($"<PaymentResponse><ResultCode>{code}</ResultCode><OrderId>ORD-1</OrderId></PaymentResponse>");
        Assert.False(o.IsApproved);
        Assert.Equal(expected, o.DeclineClass);
    }

    [Fact]
    public void Parse_BozukXml_Red()
    {
        var o = TuruncuResponseParser.Parse("not-xml");
        Assert.False(o.IsApproved);
        Assert.Equal("UNKNOWN", o.RawCode);
    }
}
