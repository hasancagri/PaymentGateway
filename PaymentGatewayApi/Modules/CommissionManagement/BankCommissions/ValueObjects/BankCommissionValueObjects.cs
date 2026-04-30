using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Enums;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;

public sealed record BankCommissionId(Guid Value)
{
    public static BankCommissionId New()            => new(Guid.NewGuid());
    public static BankCommissionId From(Guid value) => new(value);
    public override string ToString()               => Value.ToString();
}

public sealed record CommissionRate
{
    public decimal Value { get; }

    public CommissionRate(decimal value)
    {
        if (value < 0 || value > 100)
            throw new DomainException("Commission rate must be between 0 and 100.");

        Value = Math.Round(value, 4);
    }

    public override string ToString() => $"{Value}%";
}

public sealed record CommissionCriteria
{
    public CardBrand         CardBrand         { get; }
    public CardType          CardType          { get; }
    public TransactionRegion TransactionRegion { get; }

    public CommissionCriteria(
        CardBrand         cardBrand,
        CardType          cardType,
        TransactionRegion transactionRegion)
    {
        CardBrand         = cardBrand;
        CardType          = cardType;
        TransactionRegion = transactionRegion;
    }
}
