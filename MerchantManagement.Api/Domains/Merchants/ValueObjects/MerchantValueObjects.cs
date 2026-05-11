namespace MerchantManagement.Api.Domains.Merchants.ValueObjects;

public sealed record MerchantName
{
    public string Value { get; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantName(string value) => Value = value;

    public static ResultDomain<MerchantName> Create(string value)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(new MessageItem { Code = "MerchantName.Empty" });
        else if (value.Length > 100)
            errors.Add(new MessageItem { Code = "MerchantName.TooLong" });
        if (errors.Count > 0) return ResultDomain<MerchantName>.Error(errors);
        return ResultDomain<MerchantName>.Ok(new MerchantName(value.Trim()));
    }

    public static MerchantName FromPersistence(string value) => new(value);
    public override string ToString() => Value;
}

public sealed record ContactInfo
{
    public string Email { get; }
    public string Phone { get; }

    [Newtonsoft.Json.JsonConstructor]
    private ContactInfo(string email, string phone)
    {
        Email = email;
        Phone = phone;
    }

    public static ResultDomain<ContactInfo> Create(string email, string phone)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            errors.Add(new MessageItem { Code = "ContactInfo.InvalidEmail" });
        if (string.IsNullOrWhiteSpace(phone))
            errors.Add(new MessageItem { Code = "ContactInfo.PhoneEmpty" });
        if (errors.Count > 0) return ResultDomain<ContactInfo>.Error(errors);
        return ResultDomain<ContactInfo>.Ok(new ContactInfo(email.Trim().ToLowerInvariant(), phone.Trim()));
    }
}

public sealed record MerchantAddress
{
    public string Country { get; }
    public string City { get; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantAddress(string country, string city)
    {
        Country = country;
        City = city;
    }

    public static ResultDomain<MerchantAddress> Create(string country, string city)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
            errors.Add(new MessageItem { Code = "MerchantAddress.InvalidCountry" });
        if (string.IsNullOrWhiteSpace(city))
            errors.Add(new MessageItem { Code = "MerchantAddress.CityEmpty" });
        if (errors.Count > 0) return ResultDomain<MerchantAddress>.Error(errors);
        return ResultDomain<MerchantAddress>.Ok(new MerchantAddress(country.Trim().ToUpperInvariant(), city.Trim()));
    }
}

public sealed record Mcc
{
    public string Value { get; }

    [Newtonsoft.Json.JsonConstructor]
    private Mcc(string value) => Value = value;

    public static ResultDomain<Mcc> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 4 || !value.All(char.IsDigit))
            return ResultDomain<Mcc>.Error(new MessageItem { Code = "Mcc.Invalid" });
        return ResultDomain<Mcc>.Ok(new Mcc(value));
    }

    public static Mcc FromPersistence(string value) => new(value);
    public override string ToString() => Value;
}

public sealed record Currency
{
    private static readonly HashSet<string> _validCodes = ["USD", "EUR", "TRY", "GBP", "AED", "SAR"];

    public string Code { get; }

    [Newtonsoft.Json.JsonConstructor]
    private Currency(string code) => Code = code;

    public static ResultDomain<Currency> Create(string code)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(code) || code.Length != 3)
            errors.Add(new MessageItem { Code = "Currency.InvalidFormat" });
        else if (!_validCodes.Contains(code.ToUpperInvariant()))
            errors.Add(new MessageItem { Code = "Currency.Unsupported", Params = [code] });
        if (errors.Count > 0) return ResultDomain<Currency>.Error(errors);
        return ResultDomain<Currency>.Ok(new Currency(code.ToUpperInvariant()));
    }

    public static Currency FromPersistence(string code) => new(code);
    public override string ToString() => Code;
}

public sealed record ApiKeyValue
{
    public string PlainText { get; }
    public string Hash { get; }

    public static ApiKeyValue Generate()
    {
        var plain = $"pfk_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}";
        return new ApiKeyValue(plain, HashKey(plain));
    }

    public static ApiKeyValue FromHash(string hash) => new(null!, hash);

    [Newtonsoft.Json.JsonConstructor]
    private ApiKeyValue(string plainText, string hash)
    {
        PlainText = plainText;
        Hash = hash;
    }

    public bool Verify(string candidate) => Hash == HashKey(candidate);

    private static string HashKey(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}

public sealed record WebhookUrl
{
    public string Value { get; }

    [Newtonsoft.Json.JsonConstructor]
    private WebhookUrl(string value) => Value = value;

    public static ResultDomain<WebhookUrl> Create(string value)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(new MessageItem { Code = "WebhookUrl.Empty" });
        else if (value.Length > 500)
            errors.Add(new MessageItem { Code = "WebhookUrl.TooLong" });
        else if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            errors.Add(new MessageItem { Code = "WebhookUrl.InvalidUrl" });
        if (errors.Count > 0) return ResultDomain<WebhookUrl>.Error(errors);
        return ResultDomain<WebhookUrl>.Ok(new WebhookUrl(value.Trim()));
    }

    public static WebhookUrl FromPersistence(string value) => new(value);
    public override string ToString() => Value;
}