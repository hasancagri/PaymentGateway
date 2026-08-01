using Commission.Api.Domains.SharedKernel;
using Xunit;

namespace Commission.Api.Tests;

/// <summary>
/// Bulk upsert handler'ın saf çekirdeği: aynı kombinasyonun tek kayda düşmesi Criteria değer
/// eşitliğine dayanır (kopya oluşmasın). HTTP/session'sız doğrulanır.
/// </summary>
public class BulkUpsertCriteriaMatchTests
{
    private static Criteria FromCodes(string brand, string type, string region, int installment) =>
        Criteria.FromCodes(brand, type, region, installment).Data!;

    [Fact]
    public void Ayni_kombinasyon_esit_ve_ayni_hash()
    {
        var a = FromCodes("VISA", "CREDIT", "DOMESTIC", 3);
        var b = FromCodes("visa", "credit", "domestic", 3); // case-insensitive parse

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // Dictionary anahtarı olarak upsert takibi (handler'daki `touched` mantığı).
        var seen = new Dictionary<Criteria, decimal> { [a] = 1.75m };
        Assert.True(seen.ContainsKey(b));
    }

    [Theory]
    [InlineData("MASTERCARD", "CREDIT", "DOMESTIC", 3)]
    [InlineData("VISA", "DEBIT", "DOMESTIC", 3)]
    [InlineData("VISA", "CREDIT", "INTERNATIONAL", 3)]
    [InlineData("VISA", "CREDIT", "DOMESTIC", 6)]
    public void Farkli_eksen_farkli_kayit(string brand, string type, string region, int installment)
    {
        var baseline = FromCodes("VISA", "CREDIT", "DOMESTIC", 3);
        var other = FromCodes(brand, type, region, installment);

        Assert.NotEqual(baseline, other);
    }
}