namespace IAM.Api.Domains.Users.ValueObjects;

public sealed record Email
{
    public string Value { get; }
    [Newtonsoft.Json.JsonConstructor]
    private Email(string value) => Value = value;

    public static ResultDomain<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@'))
            return ResultDomain<Email>.Error(new MessageItem { Code = "Email.Invalid" });
        return ResultDomain<Email>.Ok(new Email(value.Trim().ToLowerInvariant()));
    }

    public static Email FromPersistence(string value) => new(value);
    public override string ToString() => Value;
}