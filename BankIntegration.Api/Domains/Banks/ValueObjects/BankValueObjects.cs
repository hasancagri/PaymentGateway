namespace BankIntegration.Api.Domains.Banks.ValueObjects;

public sealed record BankName
{
    public string Value { get; }
    [Newtonsoft.Json.JsonConstructor]
    private BankName(string value) => Value = value;

    public static ResultDomain<BankName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ResultDomain<BankName>.Error(new MessageItem { Code = "BankName.Empty" });
        return ResultDomain<BankName>.Ok(new BankName(value.Trim()));
    }
}

public sealed record BankPriority
{
    public int Value { get; }
    [Newtonsoft.Json.JsonConstructor]
    private BankPriority(int value) => Value = value;

    public static ResultDomain<BankPriority> Create(int value)
    {
        if (value < 1)
            return ResultDomain<BankPriority>.Error(new MessageItem { Code = "BankPriority.MustBePositive" });
        return ResultDomain<BankPriority>.Ok(new BankPriority(value));
    }
}

public sealed record BankApiUrl
{
    public string Value { get; }
    [Newtonsoft.Json.JsonConstructor]
    private BankApiUrl(string value) => Value = value;

    public static ResultDomain<BankApiUrl> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ResultDomain<BankApiUrl>.Error(new MessageItem { Code = "BankApiUrl.Empty" });
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
            return ResultDomain<BankApiUrl>.Error(new MessageItem { Code = "BankApiUrl.Invalid" });
        return ResultDomain<BankApiUrl>.Ok(new BankApiUrl(value.Trim()));
    }
}
