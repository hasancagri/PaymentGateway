namespace Commission.Api.Domains.CommissionDrafts.ValueObjects;

/// <summary>
/// Taslağın tek satırı: deterministik 1-tabanlı satır numarası + banka + kombinasyon + oran.
/// Satır no Excel'e aynen yazılır — "satır 37" adreslemesinin temeli (FR-014). Kombinasyon tam
/// <see cref="Criteria"/> taşır (kabulde MerchantCommission'a kopya için gerekli).
/// </summary>
public record DraftRow
{
    public int RowNo { get; private set; }
    public string BankCode { get; private set; } = string.Empty;
    public string BankName { get; private set; } = string.Empty;
    public Criteria Criteria { get; private set; } = null!;
    public decimal Rate { get; private set; }

    private DraftRow()
    {
    }

    public static DraftRow Create(int rowNo, string bankCode, string bankName, Criteria criteria, decimal rate) =>
        new()
        {
            RowNo = rowNo,
            BankCode = bankCode,
            BankName = bankName,
            Criteria = criteria,
            Rate = rate
        };

    /// <summary>Revizyonda yeni oranlı kopya (RowNo/banka/kombinasyon sabit kalır).</summary>
    public DraftRow WithRate(decimal rate) => this with { Rate = rate };
}