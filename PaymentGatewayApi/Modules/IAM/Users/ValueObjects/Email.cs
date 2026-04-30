namespace PaymentGatewayApi.Modules.IAM.Users.ValueObjects;

public sealed record Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@'))
            throw new DomainException("Invalid email address.");

        Value = value.Trim().ToLowerInvariant();
    }

    public override string ToString() => Value;
}
