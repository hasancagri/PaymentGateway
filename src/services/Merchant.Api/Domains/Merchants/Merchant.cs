namespace Merchant.Api.Domains.Merchants;

/// <summary>
/// Pazaryeri satıcısı (023) — iyzico SubMerchant sözleşmesiyle hizalı alan seti (YAGNI kırpımlı:
/// Currency/SwiftCode/SettlementDescriptionTemplate/SubMerchantExternalId yok). İş kuralları
/// (zorunlu alanlar, e-posta/IBAN biçimi, tip-uyum matrisi, statü geçişleri) burada yaşar;
/// sağlayıcı (Provider/SubMerchants) tipleri domain'e girmez. Doğrulamalar Create ve
/// UpdateDetails içinde INLINE tekrar eder (015: aggregate'te private helper yok — bilinçli).
/// </summary>
public class Merchant : AggregateRoot
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static readonly Regex IbanRegex = new(@"^TR\d{24}$", RegexOptions.Compiled);

    private Merchant()
    {
    }

    /// <summary>
    /// Makine erişim sırrı ("mk_" + Guid) — merchant OAuth istemcisinin client_secret'ı (012).
    /// Yalnız <see cref="Create"/>'te üretilir, DEĞİŞMEZ; yalnız oluşturma yanıtında bir kez
    /// döner, hiçbir sorgu yanıtında görünmez (rotasyon ayrı iş).
    /// </summary>
    public string MerchantKey { get; private set; } = string.Empty;

    public MerchantStatus Status { get; private set; } = MerchantStatus.Active;

    /// <summary>İşyeri tipi — tip-uyum matrisinin anahtarı.</summary>
    public MerchantType Type { get; private set; }

    public string Name { get; private set; } = string.Empty;
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

    /// <summary>
    /// İyzico alt-üye anahtarı — bu fazda HEP null; iyzico kayıt entegrasyonu (ayrı iş)
    /// doldurur. Boşluğu hiçbir akışı engellemez.
    /// </summary>
    public string? SubMerchantKey { get; private set; }

    /// <summary>
    /// Merchant fabrikası: zorunlu alanlar + e-posta biçimi + TR IBAN (normalize + mod-97) +
    /// tip-uyum matrisi (Personal → kimlik no; PrivateCompany → kimlik no + vergi dairesi +
    /// unvan; LimitedOrJointStockCompany → vergi dairesi + vergi no + unvan). Geçerse benzersiz
    /// kimlik + MerchantKey üretir, Active doğar.
    /// </summary>
    /// <remarks>Handler: CreateMerchantCommandHandler</remarks>
    public static ResultDomain<Merchant> Create(
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
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(Name), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(email))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(Email), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (!EmailRegex.IsMatch(email))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(Email), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });
        if (string.IsNullOrWhiteSpace(gsmNumber))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(GsmNumber), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(address))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(Address), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(contactName))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(ContactName), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(contactSurname))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(ContactSurname), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        // IBAN: normalize (boşluksuz, büyük harf) + TR biçimi + ISO 13616 mod-97 (inline — 015).
        var normalizedIban = iban is null ? string.Empty : iban.Replace(" ", string.Empty).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedIban))
            return ResultDomain<Merchant>.Error(new MessageItem
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
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(Iban), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });

        // Tip-uyum matrisi: fazla dolu alan reddedilmez, yalnız zorunluluk denetlenir (R2).
        if (type is MerchantType.Personal or MerchantType.PrivateCompany
            && string.IsNullOrWhiteSpace(identityNumber))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(IdentityNumber), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (type is MerchantType.PrivateCompany or MerchantType.LimitedOrJointStockCompany
            && string.IsNullOrWhiteSpace(taxOffice))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(TaxOffice), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (type is MerchantType.LimitedOrJointStockCompany && string.IsNullOrWhiteSpace(taxNumber))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(TaxNumber), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (type is MerchantType.PrivateCompany or MerchantType.LimitedOrJointStockCompany
            && string.IsNullOrWhiteSpace(legalCompanyTitle))
            return ResultDomain<Merchant>.Error(new MessageItem
            { Property = nameof(LegalCompanyTitle), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        return ResultDomain<Merchant>.Ok(new Merchant
        {
            MerchantKey = "mk_" + Guid.NewGuid(),
            Status = MerchantStatus.Active,
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
            LegalCompanyTitle = string.IsNullOrWhiteSpace(legalCompanyTitle) ? null : legalCompanyTitle.Trim(),
            SubMerchantKey = null
        });
    }

    /// <summary>
    /// Merchant bilgilerini günceller — Create ile aynı doğrulama seti (inline TEKRAR, bilinçli:
    /// 015 private-helper yasağı). Id / MerchantKey / Status / SubMerchantKey DEĞİŞMEZ.
    /// </summary>
    /// <remarks>Handler: UpdateMerchantCommandHandler</remarks>
    public ResultDomain UpdateDetails(
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
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Name), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(email))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Email), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (!EmailRegex.IsMatch(email))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Email), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });
        if (string.IsNullOrWhiteSpace(gsmNumber))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(GsmNumber), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(address))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Address), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(contactName))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(ContactName), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(contactSurname))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(ContactSurname), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        // IBAN: normalize (boşluksuz, büyük harf) + TR biçimi + ISO 13616 mod-97 (inline — 015).
        var normalizedIban = iban is null ? string.Empty : iban.Replace(" ", string.Empty).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedIban))
            return ResultDomain.Error(new MessageItem
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
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Iban), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });

        // Tip-uyum matrisi: fazla dolu alan reddedilmez, yalnız zorunluluk denetlenir (R2).
        if (type is MerchantType.Personal or MerchantType.PrivateCompany
            && string.IsNullOrWhiteSpace(identityNumber))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(IdentityNumber), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (type is MerchantType.PrivateCompany or MerchantType.LimitedOrJointStockCompany
            && string.IsNullOrWhiteSpace(taxOffice))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(TaxOffice), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (type is MerchantType.LimitedOrJointStockCompany && string.IsNullOrWhiteSpace(taxNumber))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(TaxNumber), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });
        if (type is MerchantType.PrivateCompany or MerchantType.LimitedOrJointStockCompany
            && string.IsNullOrWhiteSpace(legalCompanyTitle))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(LegalCompanyTitle), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        Type = type;
        Name = name.Trim();
        Email = email.Trim();
        GsmNumber = gsmNumber.Trim();
        Address = address.Trim();
        Iban = normalizedIban;
        ContactName = contactName.Trim();
        ContactSurname = contactSurname.Trim();
        IdentityNumber = string.IsNullOrWhiteSpace(identityNumber) ? null : identityNumber.Trim();
        TaxOffice = string.IsNullOrWhiteSpace(taxOffice) ? null : taxOffice.Trim();
        TaxNumber = string.IsNullOrWhiteSpace(taxNumber) ? null : taxNumber.Trim();
        LegalCompanyTitle = string.IsNullOrWhiteSpace(legalCompanyTitle) ? null : legalCompanyTitle.Trim();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Statü geçişi: üç statü arası serbest; aynı statüye geçiş idempotent no-op —
    /// <c>Ok(false)</c> (değişmedi, çağıran event yayınlamaz), gerçek değişiklik <c>Ok(true)</c>.
    /// </summary>
    /// <remarks>Handler: ChangeMerchantStatusCommandHandler</remarks>
    public ResultDomain<bool> ChangeStatus(MerchantStatus newStatus)
    {
        if (Status == newStatus)
            return ResultDomain<bool>.Ok(false);

        Status = newStatus;
        IsActive = newStatus == MerchantStatus.Active;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain<bool>.Ok(true);
    }
}
