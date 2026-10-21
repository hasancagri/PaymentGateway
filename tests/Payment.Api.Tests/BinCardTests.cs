namespace Payment.Api.Tests;

// 090 US4 (İlke VIII Domain-TDD): BIN→kart bilgisi çözümü (saf fonksiyon). Prefix eşleşme + en-uzun-prefix
// + bilinmeyen BIN → null (routing fail-open).
public class BinCardTests
{
    private static readonly IReadOnlyList<BinCard> Cards = new List<BinCard>
    {
        new() { Bin = "400000", IssuerBankCode = "SIM-BANK-3", Scheme = "Visa", CardType = "credit" },
        new() { Bin = "411111", IssuerBankCode = "SIM-BANK-1", Scheme = "Visa", CardType = "credit" },
        new() { Bin = "41111199", IssuerBankCode = "SIM-BANK-2", Scheme = "Visa", CardType = "debit" } // daha spesifik
    };

    [Fact]
    public void ResolveByPan_PrefixEslesme_IssuerDoner()
    {
        var card = BinCard.ResolveByPan(Cards, "4000000000000051");
        Assert.NotNull(card);
        Assert.Equal("SIM-BANK-3", card!.IssuerBankCode);
    }

    [Fact]
    public void ResolveByPan_EnUzunPrefix_Kazanir()
    {
        // 41111199... hem "411111" hem "41111199" ile eşleşir → en spesifik (uzun) kazanır.
        var card = BinCard.ResolveByPan(Cards, "4111119900000000");
        Assert.NotNull(card);
        Assert.Equal("SIM-BANK-2", card!.IssuerBankCode);
    }

    [Fact]
    public void ResolveByPan_BilinmeyenBin_Null()
        => Assert.Null(BinCard.ResolveByPan(Cards, "5100000000000000"));

    [Fact]
    public void ResolveByPan_BosPan_Null()
        => Assert.Null(BinCard.ResolveByPan(Cards, ""));
}
