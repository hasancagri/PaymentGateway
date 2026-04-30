namespace PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;

public sealed record RoleName
{
    public string Value { get; }

    public RoleName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Role name cannot be empty.");
        if (value.Length > 50)
            throw new DomainException("Role name cannot exceed 50 characters.");

        Value = value.Trim();
    }

    public override string ToString() => Value;
}