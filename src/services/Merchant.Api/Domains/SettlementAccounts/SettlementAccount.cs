
namespace Merchant.Api.Domains.SettlementAccounts;

/// <summary>
/// Merchant'a settlement/payout amacıyla para yatırılacak banka hesabı. Saf invariant'lar
/// (zorunlu alan, BankCode biçimi, IBAN format + mod-97) aggregate içinde; varlık kontrolleri
/// (Merchant/BankCatalog varlığı, mükerrer IBAN) handler'da. <c>MerchantId</c> ile mevcut
/// <c>Merchant</c>'a bağlıdır (referans, navigation değil).
/// </summary>
public class SettlementAccount : AggregateRoot
{
    private static readonly Regex BankCodeRegex = new(@"^\d{4}$", RegexOptions.Compiled);
    private static readonly Regex IbanRegex = new(@"^TR\d{24}$", RegexOptions.Compiled);

    private SettlementAccount()
    {
    }

    /// <summary>Sahip merchant referansı (nesne değil).</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>4 hane banka kodu; <c>BankCatalog</c>'ta bulunmalı (varlık handler'da).</summary>
    public string BankCode { get; private set; } = string.Empty;

    /// <summary>Normalize (boşluksuz, büyük harf) saklanan TR IBAN.</summary>
    public string Iban { get; private set; } = string.Empty;

    public string AccountOwnerName { get; private set; } = string.Empty;

    /// <summary>Opsiyonel banka hesap numarası.</summary>
    public string AccountNo { get; private set; } = string.Empty;

    /// <summary>Opsiyonel açıklama.</summary>
    public string AccountDescription { get; private set; } = string.Empty;

    public SettlementAccountStatus Status { get; private set; } = SettlementAccountStatus.Active;

    /// <summary>
    /// Saf format doğrulaması + IBAN normalize. Merchant/BankCatalog/mükerrer IBAN kontrolü
    /// BURADA DEĞİL — handler'da.
    /// </summary>
    /// <remarks>Handler: CreateSettlementAccountCommandHandler</remarks>
    public static ResultDomain<SettlementAccount> Create(
        Guid merchantId,
        string bankCode,
        string iban,
        string ownerName,
        string accountNo,
        string description)
    {
        var normalizedIban = NormalizeIban(iban);
        var validation = Validate(merchantId, bankCode, normalizedIban, ownerName);
        if (validation is not null)
            return ResultDomain<SettlementAccount>.Error(validation);

        return ResultDomain<SettlementAccount>.Ok(new SettlementAccount
        {
            MerchantId = merchantId,
            BankCode = bankCode,
            Iban = normalizedIban,
            AccountOwnerName = ownerName,
            AccountNo = accountNo ?? string.Empty,
            AccountDescription = description ?? string.Empty,
            Status = SettlementAccountStatus.Active
        });
    }

    /// <summary>Hesap bilgilerini günceller (Create ile aynı saf doğrulama). MerchantId değişmez.</summary>
    /// <remarks>Handler: UpdateSettlementAccountCommandHandler</remarks>
    public ResultDomain UpdateDetails(
        string bankCode,
        string iban,
        string ownerName,
        string accountNo,
        string description)
    {
        var normalizedIban = NormalizeIban(iban);
        var validation = Validate(MerchantId, bankCode, normalizedIban, ownerName);
        if (validation is not null)
            return ResultDomain.Error(validation);

        BankCode = bankCode;
        Iban = normalizedIban;
        AccountOwnerName = ownerName;
        AccountNo = accountNo ?? string.Empty;
        AccountDescription = description ?? string.Empty;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Hesabı aktif duruma alır (Status=Active, IsActive=true).</summary>
    /// <remarks>Handler: SetSettlementAccountStatusCommandHandler</remarks>
    public void Activate()
    {
        Status = SettlementAccountStatus.Active;
        IsActive = true;
        UpdatedTime = DateTime.UtcNow;
    }

    /// <summary>Pasife alır (soft — kayıt silinmez).</summary>
    /// <remarks>Handler: SetSettlementAccountStatusCommandHandler</remarks>
    public void Deactivate()
    {
        Status = SettlementAccountStatus.Passive;
        IsActive = false;
        UpdatedTime = DateTime.UtcNow;
    }

    private static MessageItem? Validate(Guid merchantId, string bankCode, string normalizedIban, string ownerName)
    {
        if (merchantId == Guid.Empty)
            return Required(nameof(MerchantId));
        if (string.IsNullOrWhiteSpace(bankCode))
            return Required(nameof(BankCode));
        if (string.IsNullOrWhiteSpace(normalizedIban))
            return Required(nameof(Iban));
        if (string.IsNullOrWhiteSpace(ownerName))
            return Required(nameof(AccountOwnerName));

        if (!BankCodeRegex.IsMatch(bankCode))
            return InvalidFormat(nameof(BankCode));

        if (!IbanRegex.IsMatch(normalizedIban) || !IsValidIbanChecksum(normalizedIban))
            return InvalidFormat(nameof(Iban));

        return null;
    }

    /// <summary>Boşlukları temizler ve büyük harfe çevirir (null-güvenli).</summary>
    private static string NormalizeIban(string iban) =>
        iban is null ? string.Empty : iban.Replace(" ", string.Empty).ToUpperInvariant();

    /// <summary>ISO 13616 mod-97 checksum: ilk 4 karakter sona taşınır, harfler 10..35, mod 97 == 1.</summary>
    private static bool IsValidIbanChecksum(string iban)
    {
        var rearranged = iban.Substring(4) + iban.Substring(0, 4);
        var remainder = 0;
        foreach (var ch in rearranged)
        {
            int value;
            if (ch >= '0' && ch <= '9')
                value = ch - '0';
            else if (ch >= 'A' && ch <= 'Z')
                value = ch - 'A' + 10;
            else
                return false;

            // İki basamaklı harf değerlerini basamak basamak işle (taşma olmadan).
            remainder = value > 9
                ? (remainder * 100 + value) % 97
                : (remainder * 10 + value) % 97;
        }

        return remainder == 1;
    }

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
}

/// <summary>
/// Settlement hesabının yaşam döngüsü durumu. Düz enum (mevcut <c>MerchantStatus</c> konvansiyonu).
/// Settlement için aktif/pasif yeterli — Suspended yok (YAGNI).
/// </summary>
public enum SettlementAccountStatus
{
    Active = 1,
    Passive = 2
}