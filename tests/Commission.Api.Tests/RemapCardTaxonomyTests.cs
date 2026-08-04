using Commission.Api.Domains.BankCommissions;
using Commission.Api.Infrastructure;
using Commission.Api.Domains.SharedKernel;
using Xunit;

namespace Commission.Api.Tests;

public class RemapCardTaxonomyTests
{
    // Eski int'i taşıyan Criteria kurar (Create enum doğrulamaz → ham int castlenebilir).
    private static Criteria Legacy(int brandInt, int typeInt) =>
        Criteria.Create((CardBrand)brandInt, (CardType)typeInt, TransactionRegion.DOMESTIC, 6).Data!;

    [Theory]
    [InlineData(1, CardBrand.Visa)]        // VISA(1)  → 0
    [InlineData(2, CardBrand.MasterCard)]  // MASTER(2)→ 1
    [InlineData(3, CardBrand.Troy)]        // TROY(3)  → 2
    [InlineData(4, CardBrand.Amex)]        // AMEX(4)  → 3
    public void Remap_marka_eski_int_dogru_kanonige(int legacyInt, CardBrand expected)
    {
        var result = CardTaxonomyRemap.Remap(Legacy(legacyInt, 1));

        Assert.Equal(expected, result.CardBrand);
    }

    [Theory]
    [InlineData(1, CardType.Credit)]   // CREDIT(1) → 1
    [InlineData(2, CardType.Debit)]    // DEBIT(2)  → 0
    [InlineData(3, CardType.Prepaid)]  // PREPAID(3)→ 2 (kaybolmaz)
    public void Remap_tip_eski_int_dogru_kanonige(int legacyInt, CardType expected)
    {
        var result = CardTaxonomyRemap.Remap(Legacy(1, legacyInt));

        Assert.Equal(expected, result.CardType);
    }

    [Fact]
    public void Remap_bolge_ve_taksit_korunur()
    {
        var legacy = Criteria.Create((CardBrand)2, (CardType)2, TransactionRegion.INTERNATIONAL, 9).Data!;

        var result = CardTaxonomyRemap.Remap(legacy);

        Assert.Equal(TransactionRegion.INTERNATIONAL, result.TransactionRegion);
        Assert.Equal(9, result.InstallmentCount);
    }

    [Fact]
    public void Yeni_kayit_zaten_kanonik_migration_atlar()
    {
        // Create ile üretilen doküman güncel sürümle doğar → migration (version < güncel) onu işlemez.
        var doc = BankCommission.Create("0062", Criteria.Create(CardBrand.Visa, CardType.Credit,
            TransactionRegion.DOMESTIC, 6).Data!, 1.5m).Data!;

        Assert.Equal(CardTaxonomyRemap.CurrentVersion, doc.TaxonomyVersion);
        Assert.False(doc.TaxonomyVersion < CardTaxonomyRemap.CurrentVersion);
    }

    [Fact]
    public void MigrateTaxonomy_criteria_gunceller_ve_isaretler()
    {
        var doc = BankCommission.Create("0062", Legacy(2, 3), 1.5m).Data!;

        doc.MigrateTaxonomy(CardTaxonomyRemap.Remap(doc.Criteria));

        Assert.Equal(CardBrand.MasterCard, doc.Criteria.CardBrand); // 2→1
        Assert.Equal(CardType.Prepaid, doc.Criteria.CardType);      // 3→2
        Assert.Equal(CardTaxonomyRemap.CurrentVersion, doc.TaxonomyVersion);
    }
}

public class CriteriaBackwardCompatTests
{
    [Theory]
    [InlineData("VISA", CardBrand.Visa)]         // eski büyük-harf string hâlâ parse eder
    [InlineData("Visa", CardBrand.Visa)]
    [InlineData("MASTERCARD", CardBrand.MasterCard)]
    public void FromCodes_eski_marka_string_case_insensitive(string code, CardBrand expected)
    {
        var result = Criteria.FromCodes(code, "CREDIT", "DOMESTIC", 6);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Data!.CardBrand);
    }

    [Theory]
    [InlineData("CREDIT", CardType.Credit)]
    [InlineData("DEBIT", CardType.Debit)]
    [InlineData("PREPAID", CardType.Prepaid)]
    public void FromCodes_eski_tip_string_case_insensitive(string code, CardType expected)
    {
        var result = Criteria.FromCodes("VISA", code, "DOMESTIC", 6);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Data!.CardType);
    }
}