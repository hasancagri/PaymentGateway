namespace PaymentGatewayApi.Modules.BankIntegration.Banks.ValueObjects;

public sealed record BankName
{
    public string Value { get; }
    private BankName(string value) => Value = value;

    public static ResultDomain<BankName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ResultDomain<BankName>.Error(new MessageItem { Code = "BankName.Empty" });
        return ResultDomain<BankName>.Ok(new BankName(value.Trim()));
    }

    public static BankName FromPersistence(string value) => new(value);
    public override string ToString() => Value;
}

public sealed record BankPriority
{
    public int Value { get; }
    private BankPriority(int value) => Value = value;

    public static ResultDomain<BankPriority> Create(int value)
    {
        if (value < 1)
            return ResultDomain<BankPriority>.Error(new MessageItem { Code = "BankPriority.MustBePositive" });
        return ResultDomain<BankPriority>.Ok(new BankPriority(value));
    }

    public static BankPriority FromPersistence(int value) => new(value);
}