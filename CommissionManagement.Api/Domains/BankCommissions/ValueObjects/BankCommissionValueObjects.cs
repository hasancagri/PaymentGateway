using CommissionManagement.Api.Domains.BankCommissions.Enums;
using Common.Domains;

namespace CommissionManagement.Api.Domains.BankCommissions.ValueObjects;

public sealed record CommissionRate
{
    public decimal Value { get; }
    [Newtonsoft.Json.JsonConstructor]
    private CommissionRate(decimal value) => Value = value;

    public static ResultDomain<CommissionRate> Create(decimal value)
    {
        if (value is < 0 or > 100)
        {
            return ResultDomain<CommissionRate>.Error(new MessageItem { Code = "CommissionRate.OutOfRange" });
        }

        return ResultDomain<CommissionRate>.Ok(new CommissionRate(Math.Round(value, 4)));
    }

    public static CommissionRate FromPersistence(decimal value) => new(value);
    public override string ToString() => $"{Value}%";
}

public sealed record CommissionCriteria
{
    public CardBrand CardBrand { get; }
    public CardType CardType { get; }
    public TransactionRegion TransactionRegion { get; }

    [Newtonsoft.Json.JsonConstructor]
    public CommissionCriteria(CardBrand cardBrand, CardType cardType, TransactionRegion transactionRegion)
    {
        CardBrand = cardBrand;
        CardType = cardType;
        TransactionRegion = transactionRegion;
    }

    public static CommissionCriteria FromPersistence(CardBrand cardBrand, CardType cardType,
        TransactionRegion transactionRegion)
        => new(cardBrand, cardType, transactionRegion);
}