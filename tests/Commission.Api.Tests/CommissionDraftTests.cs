namespace Commission.Api.Tests;

/// <summary>019 US1/US3 — taslak üretimi (marj/deterministik sıra/satır no) + revizyon (set/delta/taban bekçisi).</summary>
public class CommissionDraftTests
{
    private static Criteria Cr(int installment, CardBrand brand = CardBrand.Visa,
        CardType type = CardType.Credit, TransactionRegion region = TransactionRegion.DOMESTIC) =>
        Criteria.Create(brand, type, region, installment).Data!;

    private static CommissionDraft NewDraft(decimal margin = 0.5m)
    {
        // Karışık sırada verilir — deterministik sıralama (BankCode ASC → Taksit ASC) doğrulanır.
        var rows = new List<BankGridSourceRow>
        {
            new("0064", "İş Bankası", Cr(6), 2.10m),
            new("0046", "Akbank", Cr(1), 1.75m),
            new("0046", "Akbank", Cr(6), 2.00m),
            new("0062", "Garanti", Cr(9), 2.05m),
        };
        return CommissionDraft.CreateFromBankGrid(Guid.NewGuid(), rows, margin).Data!;
    }

    [Fact]
    public void CreateFromBankGrid_marj_ekler_ve_deterministik_siralar()
    {
        var draft = NewDraft(margin: 0.5m);

        Assert.Equal(4, draft.Rows.Count);
        Assert.False(draft.IsLocked);

        // Sıra: BankCode ASC (0046 < 0062 < 0064), aynı bankada taksit ASC; RowNo 1-tabanlı.
        Assert.Equal([1, 2, 3, 4], draft.Rows.Select(r => r.RowNo));
        Assert.Equal(["0046", "0046", "0062", "0064"], draft.Rows.Select(r => r.BankCode));
        Assert.Equal(1, draft.Rows[0].Criteria.InstallmentCount);
        Assert.Equal(6, draft.Rows[1].Criteria.InstallmentCount);

        // Oran = banka oranı + marj.
        Assert.Equal(2.25m, draft.Rows[0].Rate);
        Assert.Equal(2.50m, draft.Rows[1].Rate);
        Assert.Equal(2.55m, draft.Rows[2].Rate);
        Assert.Equal(2.60m, draft.Rows[3].Rate);
    }

    [Fact]
    public void CreateFromBankGrid_bos_grid_Error()
    {
        var result = CommissionDraft.CreateFromBankGrid(Guid.NewGuid(), [], 0.5m);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND);
    }

    [Fact]
    public void Revise_satir_no_set_diff_doner()
    {
        var draft = NewDraft();

        var result = draft.Revise([new DraftOperation { Op = "set", Row = 3, Rate = 1.85m }],
            new Dictionary<(string, Criteria), decimal>());

        Assert.True(result.IsSuccess);
        var change = Assert.Single(result.Data!);
        Assert.Equal(3, change.RowNo);
        Assert.Equal(2.55m, change.OldRate);
        Assert.Equal(1.85m, change.NewRate);
        Assert.Equal(1.85m, draft.Rows.Single(r => r.RowNo == 3).Rate);
    }

    [Fact]
    public void Revise_banka_taksit_set_ad_veya_kodla_eslesir()
    {
        var draft = NewDraft();

        var result = draft.Revise([new DraftOperation { Op = "set", Bank = "akbank", Installment = 6, Rate = 2.40m }],
            new Dictionary<(string, Criteria), decimal>());

        Assert.True(result.IsSuccess);
        var change = Assert.Single(result.Data!);
        Assert.Equal(2, change.RowNo);
        Assert.Equal(2.40m, draft.Rows.Single(r => r.RowNo == 2).Rate);
    }

    [Fact]
    public void Revise_filter_delta_toplu_dusurur()
    {
        var draft = NewDraft();

        var result = draft.Revise(
            [new DraftOperation { Op = "delta", Filter = new DraftOperationFilter { Installment = 6 }, Delta = -0.2m }],
            new Dictionary<(string, Criteria), decimal>());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count); // iki bankanın 6 taksidi
        Assert.Equal(2.30m, draft.Rows.Single(r => r.RowNo == 2).Rate);
        Assert.Equal(2.40m, draft.Rows.Single(r => r.RowNo == 4).Rate);
    }

    [Fact]
    public void Revise_filter_bank_set_toplu_atar()
    {
        var draft = NewDraft();

        var result = draft.Revise(
            [new DraftOperation { Op = "set", Filter = new DraftOperationFilter { Bank = "Akbank" }, Rate = 1.90m }],
            new Dictionary<(string, Criteria), decimal>());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        Assert.All(draft.Rows.Where(r => r.BankCode == "0046"), r => Assert.Equal(1.90m, r.Rate));
    }

    [Fact]
    public void Revise_taban_ihlali_butun_RET_taslak_degismez()
    {
        var draft = NewDraft(); // Akbank 6 taksit satır 2: 2.50 (banka 2.00)
        var floors = new Dictionary<(string, Criteria), decimal>
        {
            [("0046", Cr(6))] = 2.00m,
            [("0046", Cr(1))] = 1.75m,
        };

        // İki işlem: geçerli bir set + taban altına inen bir set → HİÇBİRİ uygulanmaz.
        var result = draft.Revise(
        [
            new DraftOperation { Op = "set", Row = 1, Rate = 2.10m },
            new DraftOperation { Op = "set", Row = 2, Rate = 1.80m }
        ], floors);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Code == CommissionResourceConstants.RATE_BELOW_BANK_FLOOR);
        Assert.Equal(2.25m, draft.Rows.Single(r => r.RowNo == 1).Rate); // geçerli olan da uygulanmadı
        Assert.Equal(2.50m, draft.Rows.Single(r => r.RowNo == 2).Rate);
    }

    [Fact]
    public void Revise_gecersiz_satir_Error_taslak_degismez()
    {
        var draft = NewDraft();

        var result = draft.Revise([new DraftOperation { Op = "set", Row = 999, Rate = 1.85m }],
            new Dictionary<(string, Criteria), decimal>());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND);
        Assert.Equal(2.25m, draft.Rows.Single(r => r.RowNo == 1).Rate);
    }

    [Fact]
    public void Revise_bilinmeyen_banka_taksit_Error()
    {
        var draft = NewDraft();

        var result = draft.Revise([new DraftOperation { Op = "set", Bank = "Ziraat", Installment = 6, Rate = 2.0m }],
            new Dictionary<(string, Criteria), decimal>());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND);
    }

    [Fact]
    public void Revise_kilitli_Error()
    {
        var draft = NewDraft();
        draft.Lock();

        var result = draft.Revise([new DraftOperation { Op = "set", Row = 1, Rate = 2.5m }],
            new Dictionary<(string, Criteria), decimal>());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Code == CommissionResourceConstants.DRAFT_LOCKED);
    }

    [Fact]
    public void Revise_sifir_alti_oran_butun_RET()
    {
        var draft = NewDraft();

        var result = draft.Revise(
            [new DraftOperation { Op = "delta", Filter = new DraftOperationFilter { Bank = "Akbank" }, Delta = -5m }],
            new Dictionary<(string, Criteria), decimal>());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE);
        Assert.Equal(2.25m, draft.Rows.Single(r => r.RowNo == 1).Rate);
    }

    [Fact]
    public void Lock_idempotent()
    {
        var draft = NewDraft();

        Assert.True(draft.Lock().IsSuccess);
        Assert.True(draft.Lock().IsSuccess);
        Assert.True(draft.IsLocked);
    }
}