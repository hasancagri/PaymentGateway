namespace Commission.Api.Domains.MerchantCommissions;

/// <summary>
/// Merchant grid'inin hafif başlık kaydı (013 US4/B kararı). BulkUpsert hücreleri Draft'ta doldurur
/// (kısmi olabilir); <b>Finalize</b> bütünlüğü doğrulayınca <see cref="GridStatus.Ready"/>'e geçer ve
/// <c>MerchantCommissionGridReady</c> yayınlanır (Active koşulu #2). Sürümleme YOK (014). Kabul/ret YOK
/// (gateway-otoriter). Marten kimliği = MerchantId.
/// </summary>
public class MerchantCommissionGrid
{
    private MerchantCommissionGrid()
    {
    }

    /// <summary>Marten kimliği (Program.cs Identity = MerchantId).</summary>
    public Guid MerchantId { get; private set; }

    public GridStatus Status { get; private set; } = GridStatus.Draft;

    public DateTime? FinalizedAtUtc { get; private set; }

    public static MerchantCommissionGrid CreateDraft(Guid merchantId) => new()
    {
        MerchantId = merchantId,
        Status = GridStatus.Draft
    };

    /// <summary>Bütünlük doğrulaması handler'da yapılır; burada yalnız Ready'e geçiş (idempotent).</summary>
    public void MarkReady()
    {
        if (Status == GridStatus.Ready)
            return;
        Status = GridStatus.Ready;
        FinalizedAtUtc = DateTime.UtcNow;
    }
}

public enum GridStatus
{
    Draft = 1,
    Ready = 2
}
