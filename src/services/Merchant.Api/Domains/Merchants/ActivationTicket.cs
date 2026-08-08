namespace Merchant.Api.Domains.Merchants;

/// <summary>
/// Tek-kullanımlık, süreli key-teslim bileti (013 D4). Onayla merchant oluşunca üretilir; Identity
/// aktivasyon sayfası bunu redeem eder → MerchantKey bir kez gösterilir. İkinci redeem RET (key
/// yeniden gösterilmez, FR-009). Süre dolmuş → Expired (admin yeni bilet üretebilir).
/// </summary>
public class ActivationTicket : AggregateRoot
{
    private ActivationTicket()
    {
    }

    /// <summary>Bilet TTL'i (saat).</summary>
    public const int TtlHours = 24;

    public Guid MerchantId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public ActivationTicketStatus Status { get; private set; } = ActivationTicketStatus.Issued;
    public DateTime? RedeemedAtUtc { get; private set; }

    public static ActivationTicket Issue(Guid merchantId) => new()
    {
        MerchantId = merchantId,
        Token = Guid.NewGuid().ToString("N"),
        ExpiresAtUtc = DateTime.UtcNow.AddHours(TtlHours),
        Status = ActivationTicketStatus.Issued
    };

    /// <summary>
    /// Bileti kullanır: süre + tek-kullanım. Başarı → Redeemed (key bir kez teslim). İkinci redeem
    /// veya süre dolmuş → RET (beklenen hata Result). Süre dolmuşsa Expired'e geçer.
    /// </summary>
    public ResultDomain Redeem(DateTime nowUtc)
    {
        if (Status == ActivationTicketStatus.Redeemed)
            return ResultDomain.Error(Invalid());

        if (Status == ActivationTicketStatus.Expired || nowUtc > ExpiresAtUtc)
        {
            Status = ActivationTicketStatus.Expired;
            UpdatedTime = DateTime.UtcNow;
            return ResultDomain.Error(Invalid());
        }

        Status = ActivationTicketStatus.Redeemed;
        RedeemedAtUtc = nowUtc;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    private static MessageItem Invalid() => new()
    {
        Property = nameof(Status),
        Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
    };
}

public enum ActivationTicketStatus
{
    Issued = 1,
    Redeemed = 2,
    Expired = 3
}
