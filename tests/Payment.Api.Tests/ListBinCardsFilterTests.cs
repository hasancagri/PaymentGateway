using Payment.Api.Domains.BinCards.Features.Queries;

namespace Payment.Api.Tests;

public class ListBinCardsFilterTests
{
    private static ListBinCards.ListBinCardsQuery Q(
        string? bankCode = null, string? program = null, string? type = null,
        string? brand = null, bool? commercial = null, int page = 1, int pageSize = 25) =>
        new(bankCode, program, type, brand, commercial, page, pageSize);

    [Fact]
    public void PlanFilter_gecerli_enum_adlari_parse_edilir()
    {
        var plan = ListBinCards.PlanFilter(Q(program: "bonus", type: "Credit", brand: "Troy"));

        Assert.Equal(CardProgram.Bonus, plan.CardProgram);
        Assert.Equal(CardType.Credit, plan.CardType);
        Assert.Equal(CardBrand.Troy, plan.CardBrand);
    }

    [Fact]
    public void PlanFilter_taninmayan_enum_null_olur_filtre_uygulanmaz()
    {
        var plan = ListBinCards.PlanFilter(Q(program: "yok", type: "abc", brand: "123"));

        Assert.Null(plan.CardProgram);
        Assert.Null(plan.CardType);
        Assert.Null(plan.CardBrand);
    }

    [Fact]
    public void PlanFilter_bankCode_bosluk_null_olur_trim_edilir()
    {
        Assert.Null(ListBinCards.PlanFilter(Q(bankCode: "  ")).BankCode);
        Assert.Equal("0062", ListBinCards.PlanFilter(Q(bankCode: " 0062 ")).BankCode);
    }

    [Fact]
    public void PlanFilter_commercial_aynen_tasinir()
    {
        Assert.True(ListBinCards.PlanFilter(Q(commercial: true)).Commercial);
        Assert.Null(ListBinCards.PlanFilter(Q(commercial: null)).Commercial);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void PlanFilter_page_1den_kucuk_ise_1(int input, int expected) =>
        Assert.Equal(expected, ListBinCards.PlanFilter(Q(page: input)).Page);

    [Theory]
    [InlineData(0, ListBinCards.DefaultPageSize)]   // geçersiz → varsayılan
    [InlineData(-1, ListBinCards.DefaultPageSize)]
    [InlineData(25, 25)]
    [InlineData(1000, ListBinCards.MaxPageSize)]    // aşırı → üst sınır
    public void ClampPageSize_sinirlar(int input, int expected) =>
        Assert.Equal(expected, ListBinCards.ClampPageSize(input));
}