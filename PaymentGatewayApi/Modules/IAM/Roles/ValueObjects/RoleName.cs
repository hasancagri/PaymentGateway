namespace PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;

public sealed record RoleName
{
    public string Value { get; }
    private RoleName(string value) => Value = value;

    public static ResultDomain<RoleName> Create(string value)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(new MessageItem { Code = "RoleName.Empty" });
        else if (value.Length > 50)
            errors.Add(new MessageItem { Code = "RoleName.TooLong" });
        if (errors.Count > 0) return ResultDomain<RoleName>.Error(errors);
        return ResultDomain<RoleName>.Ok(new RoleName(value.Trim()));
    }

    public static RoleName FromPersistence(string value) => new(value);
    public override string ToString() => Value;
}