namespace Merchant.Api.Domains.RegisterRequests;

/// <summary>
/// Merchant kayıt başvurusu (029) — ECommerce chat asistanının MCP ile açtığı talep. Alan seti
/// 023 Merchant sözleşmesiyle birebir; doğrulamalar (zorunlu alanlar, e-posta/IBAN biçimi,
/// tip-uyum matrisi) Merchant.Create'ten BİLİNÇLİ inline kopyadır (015: private helper yok,
/// aggregate'ler birbirinin metodunu çağırmaz). Statü makinesi: Pending → Approved/Rejected
/// (terminal); tarihçe silinmez. Mükerrer e-posta kontrolü handler'dadır (cross-document sorgu).
/// </summary>
public class RegisterRequest : AggregateRoot
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static readonly Regex IbanRegex = new(@"^TR\d{24}$", RegexOptions.Compiled);

    private RegisterRequest()
    {
    }

    public RegisterRequestStatus Status { get; private set; } = RegisterRequestStatus.Pending;

    /// <summary>İşyeri tipi — tip-uyum matrisinin anahtarı (023 enum'u, aynı BC).</summary>
    public MerchantType Type { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Başvuru kimliği: durum sorgusu + mükerrer kuralı bu adres üstünden (case-insensitive).</summary>
    public string Email { get; private set; } = string.Empty;

    public string GsmNumber { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;

    /// <summary>Normalize (boşluksuz, büyük harf) saklanan TR IBAN (mod-97 doğrulanır).</summary>
    public string Iban { get; private set; } = string.Empty;

    public string ContactName { get; private set; } = string.Empty;
    public string ContactSurname { get; private set; } = string.Empty;

    /// <summary>Kimlik no — Personal ve PrivateCompany'de zorunlu.</summary>
    public string? IdentityNumber { get; private set; }

    /// <summary>Vergi dairesi — şirket tiplerinde zorunlu.</summary>
    public string? TaxOffice { get; private set; }

    /// <summary>Vergi no — LimitedOrJointStockCompany'de zorunlu.</summary>
    public string? TaxNumber { get; private set; }

    /// <summary>Unvan — şirket tiplerinde zorunlu.</summary>
    public string? LegalCompanyTitle { get; private set; }

    /// <summary>Red nedeni — yalnız Rejected'da dolu; durum sorgusunda karşı tarafa iletilir.</summary>
    public string? RejectReason { get; private set; }

    /// <summary>Onayda doğan merchant'ın kimliği — yalnız Approved'da dolu.</summary>
    public Guid? MerchantId { get; private set; }

    /// <summary>
    /// Başvuru fabrikası: zorunlu alanlar + e-posta biçimi + TR IBAN (normalize + mod-97) +
    /// tip-uyum matrisi (Personal → kimlik no; PrivateCompany → kimlik no + vergi dairesi +
    /// unvan; LimitedOrJointStockCompany → vergi dairesi + vergi no + unvan). Geçerse Pending doğar.
    /// </summary>
    /// <remarks>Handler: SubmitRegistrationForAgentCommandHandler</remarks>
    public static ResultDomain<RegisterRequest> Submit(
        MerchantType type,
        string name,
        string email,
        string gsmNumber,
        string address,
        string iban,
        string contactName,
        string contactSurname,
        string? identityNumber,
        string? taxOffice,
        string? taxNumber,
        string? legalCompanyTitle)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(Name), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(email))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(Email), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (!EmailRegex.IsMatch(email))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(Email), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });
        if (string.IsNullOrWhiteSpace(gsmNumber))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(GsmNumber), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(address))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(Address), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(contactName))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(ContactName), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(contactSurname))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(ContactSurname), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        // IBAN: normalize (boşluksuz, büyük harf) + TR biçimi + ISO 13616 mod-97 (inline — 015).
        var normalizedIban = iban is null ? string.Empty : iban.Replace(" ", string.Empty).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedIban))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(Iban), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        var ibanValid = IbanRegex.IsMatch(normalizedIban);
        if (ibanValid)
        {
            var rearranged = normalizedIban.Substring(4) + normalizedIban.Substring(0, 4);
            var remainder = 0;
            foreach (var ch in rearranged)
            {
                var value = ch is >= '0' and <= '9' ? ch - '0' : ch - 'A' + 10;
                remainder = value > 9
                    ? (remainder * 100 + value) % 97
                    : (remainder * 10 + value) % 97;
            }

            ibanValid = remainder == 1;
        }

        if (!ibanValid)
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(Iban), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });

        // Tip-uyum matrisi: fazla dolu alan reddedilmez, yalnız zorunluluk denetlenir.
        if (type is MerchantType.Personal or MerchantType.PrivateCompany
            && string.IsNullOrWhiteSpace(identityNumber))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(IdentityNumber), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (type is MerchantType.PrivateCompany or MerchantType.LimitedOrJointStockCompany
            && string.IsNullOrWhiteSpace(taxOffice))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(TaxOffice), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (type is MerchantType.LimitedOrJointStockCompany && string.IsNullOrWhiteSpace(taxNumber))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(TaxNumber), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (type is MerchantType.PrivateCompany or MerchantType.LimitedOrJointStockCompany
            && string.IsNullOrWhiteSpace(legalCompanyTitle))
            return ResultDomain<RegisterRequest>.Error(new MessageItem
            { Property = nameof(LegalCompanyTitle), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        return ResultDomain<RegisterRequest>.Ok(new RegisterRequest
        {
            Status = RegisterRequestStatus.Pending,
            Type = type,
            Name = name.Trim(),
            Email = email.Trim(),
            GsmNumber = gsmNumber.Trim(),
            Address = address.Trim(),
            Iban = normalizedIban,
            ContactName = contactName.Trim(),
            ContactSurname = contactSurname.Trim(),
            IdentityNumber = string.IsNullOrWhiteSpace(identityNumber) ? null : identityNumber.Trim(),
            TaxOffice = string.IsNullOrWhiteSpace(taxOffice) ? null : taxOffice.Trim(),
            TaxNumber = string.IsNullOrWhiteSpace(taxNumber) ? null : taxNumber.Trim(),
            LegalCompanyTitle = string.IsNullOrWhiteSpace(legalCompanyTitle) ? null : legalCompanyTitle.Trim()
        });
    }

    /// <summary>
    /// Başvuruyu onaylar: yalnız Pending'den; doğan merchant'ın kimliği bağlanır, statü
    /// Approved (terminal) olur. Pending değilse INVALID_OPERATION_ERROR (durum korunur).
    /// </summary>
    /// <remarks>Handler: ApproveRegisterRequestCommandHandler</remarks>
    public ResultDomain Approve(Guid merchantId)
    {
        if (Status != RegisterRequestStatus.Pending)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

        Status = RegisterRequestStatus.Approved;
        MerchantId = merchantId;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Başvuruyu reddeder: yalnız Pending'den; neden zorunludur, kayıtta saklanır ve durum
    /// sorgusunda karşı tarafa iletilir. Statü Rejected (terminal) olur.
    /// </summary>
    /// <remarks>Handler: RejectRegisterRequestCommandHandler</remarks>
    public ResultDomain Reject(string reason)
    {
        if (Status != RegisterRequestStatus.Pending)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
        if (string.IsNullOrWhiteSpace(reason))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(RejectReason), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        Status = RegisterRequestStatus.Rejected;
        RejectReason = reason.Trim();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}
