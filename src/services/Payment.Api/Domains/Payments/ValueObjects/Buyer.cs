using System.Text.RegularExpressions;

namespace Payment.Api.Domains.Payments.ValueObjects;

/// <summary>
/// Çekim alıcısı — değer nesnesi (035). iyzico wire tipinden yapısal uyarlama: ham alıcı verisini
/// doğrulayıp taşır; iyzico serileştirmesini (ToPKIRequestString) BİLMEZ (o SDK wire tipinde kalır).
/// Kalıcı DEĞİL (charge-anı transient). Handler SDK <c>Iyzico.Provider.Payments.Buyer</c> wire'ına map'ler.
/// Yapısal doğrulama şimdi (boş + e-posta formatı); zengin kural sonra.
/// </summary>
public sealed class Buyer
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private Buyer()
    {
    }

    public string Name { get; private init; } = string.Empty;
    public string Surname { get; private init; } = string.Empty;
    public string Email { get; private init; } = string.Empty;
    public string GsmNumber { get; private init; } = string.Empty;
    public string IdentityNumber { get; private init; } = string.Empty;
    public string RegistrationAddress { get; private init; } = string.Empty;
    public string City { get; private init; } = string.Empty;
    public string Country { get; private init; } = string.Empty;
    public string Ip { get; private init; } = string.Empty;

    /// <summary>
    /// Alıcı VO'su üretir. Zorunlu alanlar boş olamaz; e-posta format kontrolü; kimlik 11 hane (ince).
    /// Geçersizse <see cref="ResultDomain{T}.Error(MessageItem)"/>.
    /// </summary>
    public static ResultDomain<Buyer> Create(
        string name, string surname, string email, string gsmNumber, string identityNumber,
        string registrationAddress, string city, string country, string ip)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname) ||
            string.IsNullOrWhiteSpace(gsmNumber) || string.IsNullOrWhiteSpace(registrationAddress) ||
            string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(country) ||
            string.IsNullOrWhiteSpace(ip))
            return ResultDomain<Buyer>.Error(new MessageItem
            { Property = nameof(Buyer), Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED });

        if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            return ResultDomain<Buyer>.Error(new MessageItem
            { Property = nameof(Email), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });

        var digits = new string((identityNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != 11)
            return ResultDomain<Buyer>.Error(new MessageItem
            { Property = nameof(IdentityNumber), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT });

        return ResultDomain<Buyer>.Ok(new Buyer
        {
            Name = name,
            Surname = surname,
            Email = email,
            GsmNumber = gsmNumber,
            IdentityNumber = digits,
            RegistrationAddress = registrationAddress,
            City = city,
            Country = country,
            Ip = ip
        });
    }
}
