
namespace Merchant.Api.Domains.Merchants;

/// <summary>
/// Global merchant registry — merchant kimliğinin source of truth'u. Key üretimi/alanı YOK
/// (Identity dilimi). Alan format doğrulamaları burada (saf); MCC/Country/City varlık
/// doğrulaması handler'da (Reference read-model).
/// </summary>
public class Merchant : AggregateRoot
{
    private static readonly Regex MccRegex = new(@"^\d{4}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private Merchant()
    {
    }

    /// <summary>
    /// Gateway'in onboarding'de mint ettiği benzersiz, değişmez, açık dış kimlik (örn.
    /// "mk_9f1c..."). Yalnız <see cref="Create"/>'te atanır; hiçbir metot değiştirmez.
    /// </summary>
    public string MerchantKey { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;

    /// <summary>Lookup kodu (örn. "TR"); nesne değil.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Lookup kodu (örn. "34").</summary>
    public string CityCode { get; private set; } = string.Empty;

    /// <summary>Lookup kodu (4 hane, örn. "5411").</summary>
    public string Mcc { get; private set; } = string.Empty;

    public string WebhookUrl { get; private set; } = string.Empty;

    public MerchantStatus Status { get; private set; } = MerchantStatus.Active;

    /// <summary>Opak beyan (taxId gibi). Onboarding'de descriptor'dan kopyalanır.</summary>
    public string? TaxId { get; private set; }

    /// <summary>Ödeme dönüş adresi (geçerli HTTPS). Active koşulu #3.</summary>
    public string? ReturnUrl { get; private set; }

    /// <summary>Merchant'ın başvuruda verdiği iletişim maili (onboarding'de RegisterRequest'ten kopyalanır).</summary>
    public string? MerchantMail { get; private set; }

    /// <summary>Active koşulu #1 — en az bir settlement hesabı eklenince set (BC-içi).</summary>
    public bool HasSettlementAccount { get; private set; }

    /// <summary>Active koşulu #2 — komisyon grid hazır event'i gelince set (cross-BC).</summary>
    public bool CommissionGridReady { get; private set; }

    /// <summary>Key teslim (aktivasyon) anı; Provisioning'e geçişte set.</summary>
    public DateTime? ActivatedAtUtc { get; private set; }

    // --- Aktivasyon bileti (015: eski ActivationTicket aggregate'inden gömüldü) ---

    /// <summary>Aktivasyon bileti TTL'i (saat).</summary>
    public const int ActivationTtlHours = 24;

    /// <summary>Tek-kullanımlık key-teslim token'ı (MerchantKey'den AYRI; onayda üretilir).</summary>
    public string ActivationToken { get; private set; } = string.Empty;

    /// <summary>Aktivasyon biletinin son kullanma anı (~24 saat).</summary>
    public DateTime? ActivationExpiresAtUtc { get; private set; }

    /// <summary>Redeem anı; doluysa bilet kullanılmıştır (tek-kullanım işareti).</summary>
    public DateTime? ActivationRedeemedAtUtc { get; private set; }

    /// <summary>
    /// Saf format doğrulaması. Varlık (lookup) doğrulaması handler'da. <paramref name="merchantKey"/>
    /// handler tarafından üretilip (benzersizlik denetlenmiş) geçirilir; burada yalnız boş-değil kontrolü.
    /// </summary>
    /// <remarks>Handler: CreateMerchantCommandHandler</remarks>
    public static ResultDomain<Merchant> Create(
        string merchantKey,
        string name,
        string email,
        string phone,
        string countryCode,
        string cityCode,
        string mcc,
        string webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(merchantKey))
            return ResultDomain<Merchant>.Error(Required(nameof(MerchantKey)));

        var validation = Validate(name, email, phone, countryCode, cityCode, mcc, webhookUrl);
        if (validation is not null)
            return ResultDomain<Merchant>.Error(validation);

        return ResultDomain<Merchant>.Ok(new Merchant
        {
            MerchantKey = merchantKey,
            Name = name,
            Email = email,
            Phone = phone,
            CountryCode = countryCode,
            CityCode = cityCode,
            Mcc = mcc,
            WebhookUrl = webhookUrl,
            Status = MerchantStatus.Active
        });
    }

    /// <summary>
    /// Onboarding hattı fabrikası (013): merchant onayla <b>Provisioning</b> statüsünde doğar.
    /// Yalnız descriptor'dan gelen alanlar doğrulanır (ad + e-posta biçimi + HTTPS webhook); profil
    /// (telefon/ülke/şehir/MCC) merchant kendi token'ıyla sonradan tamamlar. Charge yetkisi yok.
    /// </summary>
    /// <remarks>Handler: ApproveRegisterRequestCommandHandler</remarks>
    public static ResultDomain<Merchant> CreateForOnboarding(
        string merchantKey,
        string name,
        string email,
        string webhookUrl,
        string? taxId,
        string? merchantMail)
    {
        if (string.IsNullOrWhiteSpace(merchantKey))
            return ResultDomain<Merchant>.Error(Required(nameof(MerchantKey)));
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Merchant>.Error(Required(nameof(Name)));
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            return ResultDomain<Merchant>.Error(InvalidFormat(nameof(Email)));
        if (!IsHttpsUrl(webhookUrl))
            return ResultDomain<Merchant>.Error(InvalidFormat(nameof(WebhookUrl)));

        return ResultDomain<Merchant>.Ok(new Merchant
        {
            MerchantKey = merchantKey,
            Name = name,
            Email = email,
            WebhookUrl = webhookUrl,
            TaxId = taxId?.Trim(),
            MerchantMail = string.IsNullOrWhiteSpace(merchantMail) ? null : merchantMail.Trim(),
            Status = MerchantStatus.Provisioning,
            IsActive = false
        });
    }

    /// <summary>
    /// Aktivasyon bileti üretir (onayda, <c>ApproveRegisterRequest</c>): tek-kullanımlık teslim token'ı
    /// + 24h TTL. Redeem anı sıfırlanır (yeni bilet kullanılmamış).
    /// </summary>
    /// <remarks>Handler: ApproveRegisterRequestCommandHandler</remarks>
    public void IssueActivation(DateTime nowUtc)
    {
        ActivationToken = Guid.NewGuid().ToString("N");
        ActivationExpiresAtUtc = nowUtc.AddHours(ActivationTtlHours);
        ActivationRedeemedAtUtc = null;
        UpdatedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Aktivasyon biletini redeem eder (Identity aktivasyon sayfasından): tek-kullanım + TTL invariant'ı.
    /// Başarıda redeem anı işaretlenir, statü Provisioning'de sabitlenir ve <see cref="ActivatedAtUtc"/>
    /// bir kez set edilir (key teslim anı). İkinci redeem veya süresi geçmiş bilet → RET (key yeniden
    /// gösterilmez, FR-009). Beklenen hata Result ile taşınır.
    /// </summary>
    /// <remarks>Handler: RedeemActivationCommandHandler</remarks>
    public ResultDomain RedeemActivation(DateTime nowUtc)
    {
        if (ActivationRedeemedAtUtc is not null)
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });

        if (ActivationExpiresAtUtc is null || nowUtc > ActivationExpiresAtUtc)
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Status),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
            });

        // Key teslim anı (eski Provision davranışı inline — ayrı public metot bırakılmaz):
        // statü Active değilse Provisioning'de sabit, ActivatedAtUtc bir kez set.
        ActivationRedeemedAtUtc = nowUtc;
        ActivatedAtUtc ??= DateTime.UtcNow;
        if (Status != MerchantStatus.Active)
            Status = MerchantStatus.Provisioning;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Ödeme dönüş adresi (HTTPS zorunlu). Active koşulu #3.</summary>
    /// <remarks>Handler: SetReturnUrlCommandHandler</remarks>
    public ResultDomain SetReturnUrl(string returnUrl)
    {
        if (!IsHttpsUrl(returnUrl))
            return ResultDomain.Error(InvalidFormat(nameof(ReturnUrl)));

        ReturnUrl = returnUrl;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Active koşulu #1 (settlement hesabı eklendi). İdempotent.</summary>
    /// <remarks>Handler: CreateSettlementAccountCommandHandler</remarks>
    public void MarkSettlementAccountPresent()
    {
        HasSettlementAccount = true;
        UpdatedTime = DateTime.UtcNow;
    }

    /// <summary>Active koşulu #2 (komisyon grid hazır). İdempotent; tek-yön (ready kalır).</summary>
    /// <remarks>Handler: MerchantCommissionGridReadyHandler</remarks>
    public void MarkCommissionGridReady()
    {
        CommissionGridReady = true;
        UpdatedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Saf iç değerlendirme (013 D10): 3 koşul (settlement + gridReady + ReturnUrl) doluysa
    /// Provisioning→Active geçer. İdempotent — zaten Active ya da koşul eksikse <c>Error</c>
    /// (geçiş yok = beklenen no-op, HTTP hatası değil). Geçiş gerçekleştiyse <c>Ok</c> döner
    /// (handler <c>IsSuccess</c> ile <c>MerchantStatusChanged(Active)</c> yayınlar).
    /// </summary>
    /// <remarks>Handler: MerchantCommissionGridReadyHandler, CreateSettlementAccountCommandHandler, SetReturnUrlCommandHandler</remarks>
    public ResultDomain TryActivate()
    {
        if (Status != MerchantStatus.Provisioning)
            return ResultDomain.Error(InvalidOperation());

        if (!HasSettlementAccount || !CommissionGridReady || string.IsNullOrWhiteSpace(ReturnUrl))
            return ResultDomain.Error(InvalidOperation());

        Status = MerchantStatus.Active;
        IsActive = true;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Statüyü Active yapar, IsActive=true.</summary>
    /// <remarks>Handler: SetMerchantStatusCommandHandler</remarks>
    public void Activate()
    {
        Status = MerchantStatus.Active;
        IsActive = true;
        UpdatedTime = DateTime.UtcNow;
    }

    /// <summary>Statüyü Passive yapar, IsActive=false.</summary>
    /// <remarks>Handler: SetMerchantStatusCommandHandler</remarks>
    public void Deactivate()
    {
        Status = MerchantStatus.Passive;
        IsActive = false;
        UpdatedTime = DateTime.UtcNow;
    }

    /// <summary>Statüyü Suspended yapar, IsActive=false.</summary>
    /// <remarks>Handler: SetMerchantStatusCommandHandler</remarks>
    public void Suspend()
    {
        Status = MerchantStatus.Suspended;
        IsActive = false;
        UpdatedTime = DateTime.UtcNow;
    }

    private static MessageItem? Validate(
        string name,
        string email,
        string phone,
        string countryCode,
        string cityCode,
        string mcc,
        string webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Required(nameof(Name));
        if (string.IsNullOrWhiteSpace(email))
            return Required(nameof(Email));
        if (string.IsNullOrWhiteSpace(phone))
            return Required(nameof(Phone));
        if (string.IsNullOrWhiteSpace(countryCode))
            return Required(nameof(CountryCode));
        if (string.IsNullOrWhiteSpace(cityCode))
            return Required(nameof(CityCode));

        if (!EmailRegex.IsMatch(email))
            return InvalidFormat(nameof(Email));
        if (string.IsNullOrWhiteSpace(mcc) || !MccRegex.IsMatch(mcc))
            return InvalidFormat(nameof(Mcc));
        if (!IsAbsoluteHttpUrl(webhookUrl))
            return InvalidFormat(nameof(WebhookUrl));

        return null;
    }

    private static bool IsAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static MessageItem Required(string property) => new()
    {
        Property = property,
        Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
    };

    private static MessageItem InvalidFormat(string property) => new()
    {
        Property = property,
        Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
    };

    private static MessageItem InvalidOperation() => new()
    {
        Property = nameof(Status),
        Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
    };
}

/// <summary>
/// Merchant yaşam döngüsü durumu. Şimdilik düz enum (kullanıcı direktifi); ileride gerekirse
/// Enumeration smart-enum'a dönüştürülür. Referans mimari de status için düz enum kullanıyor.
/// </summary>
public enum MerchantStatus
{
    Active = 1,
    Passive = 2,
    Suspended = 3,

    /// <summary>013: onboarding'de merchant onayla doğar; sınırlı yetki (charge hariç), key teslim edildi.</summary>
    Provisioning = 4
}

/// <summary>
/// merchantKey aday üreticisi (saf). Gateway'in her merchant'a mint ettiği açık dış kimlik:
/// <c>mk_</c> öneki + 32 hane hex (Guid "N"). URL-güvenli, boşluksuz, gizli DEĞİL.
/// Benzersizlik <b>garantisi</b> handler'daki üret-kontrol döngüsündedir; burada yalnız aday üretilir.
/// </summary>
public static class MerchantKeyGenerator
{
    /// <summary>Aday merchantKey üretir (mk_ + 32 hane hex); benzersizlik handler döngüsünde.</summary>
    /// <remarks>Handler: CreateMerchantCommandHandler, ApproveRegisterRequestCommandHandler</remarks>
    public static string Generate() => "mk_" + Guid.NewGuid().ToString("N");
}