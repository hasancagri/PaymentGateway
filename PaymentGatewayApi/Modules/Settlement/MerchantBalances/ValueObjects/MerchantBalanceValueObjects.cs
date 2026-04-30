namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.ValueObjects;

public sealed record MerchantBalanceId(Guid Value)
{
    public static MerchantBalanceId New()            => new(Guid.NewGuid());
    public static MerchantBalanceId From(Guid value) => new(value);
    public override string ToString()                => Value.ToString();
}

public sealed record WithdrawalId(Guid Value)
{
    public static WithdrawalId New()            => new(Guid.NewGuid());
    public static WithdrawalId From(Guid value) => new(value);
    public override string ToString()           => Value.ToString();
}
