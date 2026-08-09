namespace Merchant.Api.Domains.RegisterRequests;

/// <summary>
/// Merchant kayıt başvurusu — merchant'tan AYRI aggregate (013 D8). Merchant ancak onayla bundan
/// doğar. 015: domain-control challenge artık ayrı aggregate değil, bu talebin ALANIDIR. Talep,
/// challenge geçmeden ÖNCE <see cref="RegisterRequestStatus.AwaitingDomainControl"/> statüsünde doğar;
/// aday beklenen değeri yayınlayıp doğrulama <see cref="ChallengeOutcome.Passed"/> olunca aynı talep
/// <see cref="RegisterRequestStatus.Pending"/>'e ilerler (admin onayı bekler). Süreç tek yerden —
/// bu <see cref="Status"/> enum'undan — okunur.
/// </summary>
public class RegisterRequest : AggregateRoot
{
    private RegisterRequest()
    {
    }

    /// <summary>Challenge bileti TTL'i (saat).</summary>
    public const int ChallengeTtlHours = 1;

    /// <summary>Aday alan adı (normalize, lower) — mükerrer anahtarı.</summary>
    public string Domain { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;
    public string TaxId { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string WebhookUrl { get; private set; } = string.Empty;

    // --- Challenge alanları (015: eski DomainControlChallenge aggregate'inden gömüldü) ---

    /// <summary>Challenge dosya adı (URL-güvenli tek-kullanım); aday bu yola değeri yayınlar.</summary>
    public string ChallengeToken { get; private set; } = string.Empty;

    /// <summary>Adayın <c>/.well-known/merchant-challenge/{token}</c> yolunda yayınlaması gereken değer.</summary>
    public string ChallengeExpectedValue { get; private set; } = string.Empty;

    /// <summary>Challenge biletinin son kullanma anı (~1 saat).</summary>
    public DateTime ChallengeExpiresAtUtc { get; private set; }

    /// <summary>Son doğrulama sonucu; <see cref="ChallengeOutcome.Passed"/> talebi Pending'e taşır.</summary>
    public ChallengeOutcome ChallengeResult { get; private set; } = ChallengeOutcome.Pending;

    public RegisterRequestStatus Status { get; private set; } = RegisterRequestStatus.AwaitingDomainControl;

    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNote { get; private set; }

    /// <summary>Approved'da doğan merchant.</summary>
    public Guid? CreatedMerchantId { get; private set; }

    /// <summary>Opsiyonel opak dış referans (FR-018) — merchant'a aktarılır.</summary>
    public string? ExternalRef { get; private set; }

    /// <summary>
    /// Başvuru oluşturur (015). Talep challenge geçmeden ÖNCE
    /// <see cref="RegisterRequestStatus.AwaitingDomainControl"/> statüsünde doğar; descriptor'dan
    /// doğrulanmış alanlar kopyalanır ve ilk challenge bileti üretilir. Domain normalize.
    /// </summary>
    /// <remarks>Handler: SubmitRegistrationCommandHandler</remarks>
    public static ResultDomain<RegisterRequest> CreateAwaiting(
        string domain,
        MerchantDescriptor descriptor,
        string? externalRef = null)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            {
                Property = nameof(Domain),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });

        var now = DateTime.UtcNow;
        return ResultDomain<RegisterRequest>.Ok(new RegisterRequest
        {
            Domain = domain.Trim().ToLowerInvariant(),
            LegalName = descriptor.LegalName,
            TaxId = descriptor.TaxId,
            ContactEmail = descriptor.ContactEmail,
            WebhookUrl = descriptor.WebhookUrl,
            Status = RegisterRequestStatus.AwaitingDomainControl,
            ExternalRef = string.IsNullOrWhiteSpace(externalRef) ? null : externalRef.Trim(),
            // İlk challenge bileti inline (IssueChallenge çağrılmaz — aggregate metotları yalnız
            // handler'dan çağrılır, domain-içi çağrı yok; kod tekrarı bilinçli).
            ChallengeToken = Guid.NewGuid().ToString("N"),
            ChallengeExpectedValue = Guid.NewGuid().ToString("N"),
            ChallengeExpiresAtUtc = now.AddHours(ChallengeTtlHours),
            ChallengeResult = ChallengeOutcome.Pending
        });
    }

    /// <summary>
    /// Yeni challenge bileti üretir (token + beklenen değer + TTL). İlk oluşturmada ve süre dolunca
    /// (aynı talep üzerinde) çağrılır; talep yeniden oluşturulmaz. Sonuç <see cref="ChallengeOutcome.Pending"/>'e döner.
    /// </summary>
    /// <remarks>Handler: SubmitRegistrationCommandHandler</remarks>
    public ResultDomain IssueChallenge(DateTime nowUtc)
    {
        ChallengeToken = Guid.NewGuid().ToString("N");
        ChallengeExpectedValue = Guid.NewGuid().ToString("N");
        ChallengeExpiresAtUtc = nowUtc.AddHours(ChallengeTtlHours);
        ChallengeResult = ChallengeOutcome.Pending;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Adayın yayınladığı değeri doğrular (eski <c>DomainControlChallenge.Verify</c> mantığı). Süre +
    /// tam byte eşleşmesi. Başarı → <see cref="ChallengeOutcome.Passed"/> ve talep
    /// <see cref="RegisterRequestStatus.Pending"/>'e geçer (idempotent: zaten geçmişse yine Passed).
    /// Süre dolmuş → <see cref="ChallengeOutcome.Expired"/> (çağıran <see cref="IssueChallenge"/> ile
    /// yeniler). Değer yok/yanlış → <see cref="ChallengeOutcome.Failed"/> (talep AwaitingDomainControl kalır).
    /// </summary>
    /// <remarks>Handler: SubmitRegistrationCommandHandler</remarks>
    public ResultDomain<ChallengeOutcome> VerifyChallenge(string? fetchedValue, DateTime nowUtc)
    {
        // Zaten geçmiş (Pending'e taşınmış) — tek-kullanım idempotent.
        if (Status == RegisterRequestStatus.Pending || ChallengeResult == ChallengeOutcome.Passed)
            return ResultDomain<ChallengeOutcome>.Ok(ChallengeOutcome.Passed);

        if (nowUtc > ChallengeExpiresAtUtc)
        {
            ChallengeResult = ChallengeOutcome.Expired;
            UpdatedTime = DateTime.UtcNow;
            return ResultDomain<ChallengeOutcome>.Ok(ChallengeOutcome.Expired);
        }

        if (!string.IsNullOrWhiteSpace(fetchedValue) &&
            string.Equals(fetchedValue.Trim(), ChallengeExpectedValue, StringComparison.Ordinal))
        {
            ChallengeResult = ChallengeOutcome.Passed;
            Status = RegisterRequestStatus.Pending;
            UpdatedTime = DateTime.UtcNow;
            return ResultDomain<ChallengeOutcome>.Ok(ChallengeOutcome.Passed);
        }

        ChallengeResult = ChallengeOutcome.Failed;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain<ChallengeOutcome>.Ok(ChallengeOutcome.Failed);
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
    /// <summary>015: talep doğdu, domain-control challenge henüz geçmedi (challenge alanları bu talepte).</summary>
    AwaitingDomainControl = 0,

    /// <summary>Challenge geçti; admin onayı bekleniyor.</summary>
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// Domain-control challenge doğrulama sonucu. Talep yalnız <see cref="Passed"/> ile
/// <see cref="RegisterRequestStatus.Pending"/>'e ilerler. 015: challenge ayrı aggregate değil,
/// <see cref="RegisterRequest"/>'in alanı olduğundan bu enum da RegisterRequests altında yaşar.
/// </summary>
public enum ChallengeOutcome
{
    /// <summary>Henüz doğrulanmadı (bilet üretildi, aday değeri yayınlamadı).</summary>
    Pending = 1,

    /// <summary>Doğrulandı — aday beklenen değeri doğru yayınladı (sahiplik ispatı).</summary>
    Passed = 2,

    /// <summary>Değer yok/yanlış — aday yayınlayıp tekrar deneyebilir (bilet geçerli kalır).</summary>
    Failed = 3,

    /// <summary>Bilet süresi doldu — yeni bilet üretilip tekrar denenmeli.</summary>
    Expired = 4
}