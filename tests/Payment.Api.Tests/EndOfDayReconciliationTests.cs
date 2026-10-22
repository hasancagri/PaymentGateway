using Payment.Api.Domains.PaymentLinks.BankCharge;
using Payment.Api.Domains.PaymentLinks.Process;

namespace Payment.Api.Tests;

// Phase-1 mutabakat (İlke VII; saf, test-first): operasyon-API JSON parse + banka/PG karşılaştırma.
public class EndOfDayReconciliationTests
{
    [Fact]
    public void Parse_reads_closed_batches_from_operation_envelope()
    {
        const string json = """
        {"data":{"shopCode":"MAVI0001","batches":[
          {"batchNo":"B1","transactionCount":2,"grossTotal":300.00,"netTotal":294.00},
          {"batchNo":"B2","transactionCount":1,"grossTotal":100.00,"netTotal":98.00}]}}
        """;
        var batches = BankOperationsClient.Parse(json);
        Assert.Equal(2, batches.Count);
        Assert.Equal(3, batches.Sum(b => b.TransactionCount));
        Assert.Equal(400.00m, batches.Sum(b => b.GrossTotal));
    }

    [Fact]
    public void Parse_tolerates_empty_or_malformed_body()
    {
        Assert.Empty(BankOperationsClient.Parse(""));
        Assert.Empty(BankOperationsClient.Parse("{\"data\":{}}"));
    }

    [Fact]
    public void Compare_matches_when_counts_and_gross_align()
    {
        var batches = new List<ClosedBatch> { new("B1", 3, 400.00m, 390.00m) };
        var matched = EndOfDayReconciliation.Compare("MAVIBANK", batches, pgCount: 3, pgTotal: 400.00m);
        Assert.True(matched.Matched);

        var off = EndOfDayReconciliation.Compare("MAVIBANK", batches, pgCount: 2, pgTotal: 400.00m);
        Assert.False(off.Matched); // PG 2 vs banka 3 → gözden-geçir
        Assert.Equal(3, off.BankCount);
        Assert.Equal(2, off.PgCount);
    }
}
