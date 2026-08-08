namespace Merchant.Api.Domains.RegisterRequests;

/// <summary>
/// Merchant kayıt başvurusu — merchant'tan AYRI aggregate (013 D8). Merchant ancak onayla bundan
/// doğar. Talep yalnız challenge Passed ile oluşur (FR-003). Pending dışı talep karar alamaz.
/// Descriptor'dan doğrulanmış kopya + challenge sonucu + değerlendirme bilgisi taşır.
/// </summary>
public class RegisterRequest : AggregateRoot
{
    private RegisterRequest()
    {
    }

    /// <summary>Aday alan adı (normalize, lower) — mükerrer anahtarı.</summary>
    public string Domain { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;
    public string TaxId { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string WebhookUrl { get; private set; } = string.Empty;

    public ChallengeOutcome ChallengeResult { get; private set; }
    public RegisterRequestStatus Status { get; private set; } = RegisterRequestStatus.Pending;

    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNote { get; private set; }

    /// <summary>Approved'da doğan merchant.</summary>
    public Guid? CreatedMerchantId { get; private set; }

    /// <summary>Opsiyonel opak dış referans (FR-018) — merchant'a aktarılır.</summary>
    public string? ExternalRef { get; private set; }

    /// <summary>
    /// Başvuru oluşturur. <paramref name="challengeOutcome"/> Passed değilse talep OLUŞMAZ (FR-003).
    /// Descriptor doğrulanmış kopyadan alanlar kopyalanır. Domain normalize.
    /// </summary>
    public static ResultDomain<RegisterRequest> Create(
        string domain,
        MerchantDescriptor descriptor,
        ChallengeOutcome challengeOutcome,
        string? externalRef = null)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            {
                Property = nameof(Domain),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });

        if (challengeOutcome != ChallengeOutcome.Passed)
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            {
                Property = nameof(ChallengeResult),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });

        return ResultDomain<RegisterRequest>.Ok(new RegisterRequest
        {
            Domain = domain.Trim().ToLowerInvariant(),
            LegalName = descriptor.LegalName,
            TaxId = descriptor.TaxId,
            ContactEmail = descriptor.ContactEmail,
            WebhookUrl = descriptor.WebhookUrl,
            ChallengeResult = challengeOutcome,
            Status = RegisterRequestStatus.Pending,
            ExternalRef = string.IsNullOrWhiteSpace(externalRef) ? null : externalRef.Trim()
        });
    }

    /// <summary>Onay: yalnız Pending→Approved; doğan merchant'ı bağlar. Aksi RET (idempotent koruma).</summary>
    public ResultDomain Approve(Guid merchantId, string? note)
    {
        if (Status != RegisterRequestStatus.Pending)
            return ResultDomain.Error(InvalidState());

        Status = RegisterRequestStatus.Approved;
        CreatedMerchantId = merchantId;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNote = note?.Trim();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Ret: yalnız Pending→Rejected. Merchant oluşmaz; o domainden yeni başvuru yapılabilir.</summary>
    public ResultDomain Reject(string? note)
    {
        if (Status != RegisterRequestStatus.Pending)
            return ResultDomain.Error(InvalidState());

        Status = RegisterRequestStatus.Rejected;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNote = note?.Trim();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    private static MessageItem InvalidState() => new()
    {
        Property = nameof(Status),
        Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
    };
}

public enum RegisterRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}