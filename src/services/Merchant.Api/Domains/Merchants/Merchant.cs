using System.Text.RegularExpressions;

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

    /// <summary>
    /// Saf format doğrulaması. Varlık (lookup) doğrulaması handler'da. <paramref name="merchantKey"/>
    /// handler tarafından üretilip (benzersizlik denetlenmiş) geçirilir; burada yalnız boş-değil kontrolü.
    /// </summary>
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

/// <summary>
/// Merchant yaşam döngüsü durumu. Şimdilik düz enum (kullanıcı direktifi); ileride gerekirse
/// Enumeration smart-enum'a dönüştürülür. Referans mimari de status için düz enum kullanıyor.
/// </summary>
public enum MerchantStatus
{
    Active = 1,
    Passive = 2,
    Suspended = 3
}

/// <summary>
/// merchantKey aday üreticisi (saf). Gateway'in her merchant'a mint ettiği açık dış kimlik:
/// <c>mk_</c> öneki + 32 hane hex (Guid "N"). URL-güvenli, boşluksuz, gizli DEĞİL.
/// Benzersizlik <b>garantisi</b> handler'daki üret-kontrol döngüsündedir; burada yalnız aday üretilir.
/// </summary>
public static class MerchantKeyGenerator
{
    public static string Generate() => "mk_" + Guid.NewGuid().ToString("N");
}