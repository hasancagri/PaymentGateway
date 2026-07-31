using System.Text.RegularExpressions;

namespace Merchant.Api.Domains.Merchants;

/// <summary>
/// Global merchant registry — merchant kimliğinin source of truth'u. Key üretimi/alanı YOK
/// (Identity dilimi). Alan format doğrulamaları burada (saf); MCC/Country/City varlık
/// doğrulaması handler'da (I*Lookup).
/// </summary>
public class Merchant : AggregateRoot
{
    private static readonly Regex MccRegex = new(@"^\d{4}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private Merchant()
    {
    }

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

    /// <summary>Saf format doğrulaması. Varlık (lookup) doğrulaması handler'da.</summary>
    public static ResultDomain<Merchant> Create(
        string name,
        string email,
        string phone,
        string countryCode,
        string cityCode,
        string mcc,
        string webhookUrl)
    {
        var validation = Validate(name, email, phone, countryCode, cityCode, mcc, webhookUrl);
        if (validation is not null)
            return ResultDomain<Merchant>.Error(validation);

        return ResultDomain<Merchant>.Ok(new Merchant
        {
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

    /// <summary>Profil bilgilerini günceller (aynı format doğrulaması).</summary>
    public ResultDomain UpdateProfile(
        string name,
        string email,
        string phone,
        string countryCode,
        string cityCode,
        string mcc,
        string webhookUrl)
    {
        var validation = Validate(name, email, phone, countryCode, cityCode, mcc, webhookUrl);
        if (validation is not null)
            return ResultDomain.Error(validation);

        Name = name;
        Email = email;
        Phone = phone;
        CountryCode = countryCode;
        CityCode = cityCode;
        Mcc = mcc;
        WebhookUrl = webhookUrl;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    public void Activate()
    {
        Status = MerchantStatus.Active;
        IsActive = true;
        UpdatedTime = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Status = MerchantStatus.Passive;
        IsActive = false;
        UpdatedTime = DateTime.UtcNow;
    }

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