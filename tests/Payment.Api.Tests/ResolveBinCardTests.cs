using Payment.Api.Domains.BinCards.Features.Queries;

namespace Payment.Api.Tests;

public class ResolveBinCardTests
{
    private static BinCard Card(string bin, string bankCode, CardType type, CardProgram program) => new()
    {
        BinNumber = bin,
        BankCode = bankCode,
        CardType = type,
        CardProgram = program
    };

    // Aynı Bonus programını 0124 iki kayıtla, 0062 bir kayıtla destekler → 0124 destek-azalan başta.
    private static List<BinCard> BonusCatalog() =>
    [
        Card("365770", "0124", CardType.Credit, CardProgram.Bonus),
        Card("365771", "0124", CardType.Credit, CardProgram.Bonus),
        Card("374421", "0062", CardType.Credit, CardProgram.Bonus),
        Card("401049", "0012", CardType.Debit, CardProgram.Unknown),
        Card("402142", "0032", CardType.Credit, CardProgram.Unknown)
    ];

    [Fact]
    public void SelectTarget_tam_eslesme_bulur()
    {
        var target = ResolveBinCard.SelectTarget("365770", BonusCatalog());
        Assert.Equal("365770", target?.BinNumber);
    }

    [Fact]
    public void SelectTarget_8_hane_tam_eslesme_yoksa_ilk6ya_duser()
    {
        var target = ResolveBinCard.SelectTarget("36577012", BonusCatalog());
        Assert.Equal("365770", target?.BinNumber);
    }

    [Fact]
    public void SelectTarget_bilinmeyen_bin_null()
    {
        Assert.Null(ResolveBinCard.SelectTarget("999999", BonusCatalog()));
        Assert.Null(ResolveBinCard.SelectTarget("", BonusCatalog()));
        Assert.Null(ResolveBinCard.SelectTarget(null, BonusCatalog()));
    }

    [Fact]
    public void Resolve_bilinmeyen_bin_null_doner()
    {
        Assert.Null(ResolveBinCard.Resolve("999999", BonusCatalog()));
    }

    [Fact]
    public void Resolve_kredi_karti_taksit_bankalari_destek_azalan_kart_bankasi_basta()
    {
        // Hedef 374421 / 0062: 0062 tek destekli, ama kart bankası olduğu için başa çekilir.
        var info = ResolveBinCard.Resolve("374421", BonusCatalog());

        Assert.NotNull(info);
        Assert.Equal("0062", info!.BankCode);
        Assert.True(info.IsCreditCard);
        Assert.Equal("0062", info.InstallmentBankCodes[0]);          // kart bankası başta
        Assert.Contains("0124", info.InstallmentBankCodes);          // aynı programı destekleyen diğer banka
        Assert.Equal(2, info.InstallmentBankCodes.Count);
    }

    [Fact]
    public void Resolve_banka_karti_taksit_bankalari_bos()
    {
        var info = ResolveBinCard.Resolve("401049", BonusCatalog());

        Assert.NotNull(info);
        Assert.False(info!.IsCreditCard);
        Assert.Empty(info.InstallmentBankCodes);
    }

    [Fact]
    public void Resolve_bilinmeyen_program_taksit_bankalari_bos()
    {
        var info = ResolveBinCard.Resolve("402142", BonusCatalog());

        Assert.NotNull(info);
        Assert.True(info!.IsCreditCard);          // kredi ama program Unknown
        Assert.Empty(info.InstallmentBankCodes);
    }
}