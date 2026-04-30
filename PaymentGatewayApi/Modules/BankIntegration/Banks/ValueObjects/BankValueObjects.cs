namespace PaymentGatewayApi.Modules.BankIntegration.Banks.ValueObjects;

public sealed record BankId(Guid Value)
{
    public static BankId New()            => new(Guid.NewGuid());
    public static BankId From(Guid value) => new(value);
    public override string ToString()     => Value.ToString();
}

public sealed record BankName
{
    public string Value { get; }

    public BankName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Bank name cannot be empty.");

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

public sealed record BankPriority
{
    public int Value { get; }

    public BankPriority(int value)
    {
        if (value < 1)
            throw new DomainException("Bank priority must be greater than 0.");

        Value = value;
    }
}

