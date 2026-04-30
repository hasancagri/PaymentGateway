namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;

public sealed record MerchantId(Guid Value)
{
    public static MerchantId New() => new(Guid.NewGuid());
    public static MerchantId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public sealed record MerchantName
{
    public string Value { get; }

    public MerchantName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Merchant name cannot be empty.");
        if (value.Length > 100)
            throw new DomainException("Merchant name cannot exceed 100 characters.");

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

public sealed record ContactInfo
{
    public string Email { get; }
    public string Phone { get; }

    public ContactInfo(string email, string phone)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("Invalid email address.");
        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("Phone cannot be empty.");

        Email = email.Trim().ToLowerInvariant();
        Phone = phone.Trim();
    }
}

public sealed record MerchantAddress
{
    public string Country { get; } // ISO 3166-1 alpha-2
    public string City { get; }

    public MerchantAddress(string country, string city)
    {
        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
            throw new DomainException("Country must be a valid ISO 3166-1 alpha-2 code.");
        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("City cannot be empty.");

        Country = country.Trim().ToUpperInvariant();
        City = city.Trim();
    }
}

public sealed record Mcc
{
    public string Value { get; }

    public Mcc(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 4 || !value.All(char.IsDigit))
            throw new DomainException("MCC must be a 4-digit numeric code.");

        Value = value;
    }

    public override string ToString() => Value;
}

public sealed record IpAddress
{
    public string Value { get; }

    public IpAddress(string value)
    {
        if (!System.Net.IPAddress.TryParse(value, out _))
            throw new DomainException($"'{value}' is not a valid IP address.");

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

public sealed record Currency
{
    private static readonly HashSet<string> _validCodes =
        ["USD", "EUR", "TRY", "GBP", "AED", "SAR"];

    public string Code { get; }

    public Currency(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 3)
            throw new DomainException("Currency must be a 3-letter ISO 4217 code.");
        if (!_validCodes.Contains(code.ToUpperInvariant()))
            throw new DomainException($"Unsupported currency: {code}.");

        Code = code.ToUpperInvariant();
    }

    public override string ToString() => Code;
}

public sealed record ApiKeyValue
{
    public string PlainText { get; } // Only available at generation time
    public string Hash { get; }

    public static ApiKeyValue Generate()
    {
        var plain = $"pfk_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}";
        var hash = HashKey(plain);
        return new ApiKeyValue(plain, hash);
    }

    public static ApiKeyValue FromHash(string hash) => new(null!, hash);

    private ApiKeyValue(string plainText, string hash)
    {
        PlainText = plainText;
        Hash = hash;
    }

    public bool Verify(string candidate) => Hash == HashKey(candidate);

    private static string HashKey(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}