namespace Merchant.Api.Domains.RegisterRequests;

/// <summary>
/// Merchant kayıt başvurusu — merchant'tan AYRI aggregate (013 D8). Merchant ancak onayla bundan
/// doğar. Başvuru alanları (push-inline, 016) doğrulanınca doğrudan <see cref="RegisterRequestStatus.Pending"/>
/// statüsünde doğar; admin alanları (legalName/taxId/contactEmail) inceleyip
/// <see cref="Approve"/>/<see cref="Reject"/> eder. Otomatik onay yok. Domain-control challenge
/// KALDIRILDI: sahiplik/uygunluk denetimi admin'in insan incelemesidir.
/// </summary>
public class RegisterRequest : AggregateRoot
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private RegisterRequest()
    {
    }

    /// <summary>Aday alan adı (normalize, lower) — mükerrer anahtarı.</summary>
    public string Domain { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;
    public string TaxId { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string WebhookUrl { get; private set; } = string.Empty;

    public RegisterRequestStatus Status { get; private set; } = RegisterRequestStatus.Pending;

    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNote { get; private set; }

    /// <summary>Approved'da doğan merchant.</summary>
    public Guid? CreatedMerchantId { get; private set; }

    /// <summary>Merchant'ın başvuruda verdiği iletişim maili — onayda merchant'a aktarılır.</summary>
    public string? MerchantMail { get; private set; }

    /// <summary>
    /// Başvuru oluşturur. Aday alanları başvuruda (push-inline) gelir; burada doğrulanır (zorunlu +
    /// contactEmail geçerli e-posta + webhookUrl mutlak HTTPS) ve talep doğrudan
    /// <see cref="RegisterRequestStatus.Pending"/> (admin onayı bekler) statüsünde doğar. Domain normalize.
    /// </summary>
    /// <remarks>Handler: SubmitRegistrationCommandHandler</remarks>
    public static ResultDomain<RegisterRequest> CreatePending(
        string? domain,
        string? legalName,
        string? taxId,
        string? contactEmail,
        string? webhookUrl,
        string? merchantMail = null)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            {
                Property = nameof(Domain),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        if (string.IsNullOrWhiteSpace(legalName))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            {
                Property = nameof(LegalName),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        if (string.IsNullOrWhiteSpace(taxId))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            {
                Property = nameof(TaxId),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        if (string.IsNullOrWhiteSpace(contactEmail) || !EmailRegex.IsMatch(contactEmail))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            {
                Property = nameof(ContactEmail),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
            });
        if (string.IsNullOrWhiteSpace(webhookUrl) ||
            !Uri.TryCreate(webhookUrl, UriKind.Absolute, out var webhookUri) ||
            webhookUri.Scheme != Uri.UriSchemeHttps)
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            {
                Property = nameof(WebhookUrl),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
            });

        return ResultDomain<RegisterRequest>.Ok(new RegisterRequest
        {
            Domain = domain.Trim().ToLowerInvariant(),
            LegalName = legalName.Trim(),
            TaxId = taxId.Trim(),
            ContactEmail = contactEmail.Trim(),
            WebhookUrl = webhookUrl.Trim(),
            Status = RegisterRequestStatus.Pending,
            MerchantMail = string.IsNullOrWhiteSpace(merchantMail) ? null : merchantMail.Trim()
        });
    }

    /// <summary>Onay: yalnız Pending→Approved; doğan merchant'ı bağlar. Aksi RET (idempotent koruma).</summary>
    /// <remarks>Handler: ApproveRegisterRequestCommandHandler</remarks>
    public ResultDomain Approve(Guid merchantId, string? note)
    {
        if (Status != RegisterRequestStatus.Pending)
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });

        Status = RegisterRequestStatus.Approved;
        CreatedMerchantId = merchantId;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNote = note?.Trim();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Ret: yalnız Pending→Rejected. Merchant oluşmaz; o domainden yeni başvuru yapılabilir.</summary>
    /// <remarks>Handler: RejectRegisterRequestCommandHandler</remarks>
    public ResultDomain Reject(string? note)
    {
        if (Status != RegisterRequestStatus.Pending)
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });

        Status = RegisterRequestStatus.Rejected;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNote = note?.Trim();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}

public enum RegisterRequestStatus
{
    /// <summary>Başvuru alındı; admin onayı bekleniyor (challenge yok — doğrudan bu statüde doğar).</summary>
    Pending = 1,
    Approved = 2,
    Rejected = 3
}