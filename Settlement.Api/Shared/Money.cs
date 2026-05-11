using System.Text.Json.Serialization;

namespace PaymentGatewayApi.Shared;

public sealed record Money
{
    public decimal Amount   { get; }
    public string  Currency { get; }

    [JsonConstructor]
    [Newtonsoft.Json.JsonConstructor]
    private Money(decimal amount, string currency)
    {
        Amount   = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0, currency.ToUpperInvariant());

    public static Money FromPersistence(decimal amount, string currency) => new(amount, currency);

    public static ResultDomain<Money> Create(decimal amount, string currency)
    {
        var errors = new List<MessageItem>();
        if (amount < 0)
            errors.Add(new MessageItem { Code = "Money.NegativeAmount" });
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            errors.Add(new MessageItem { Code = "Money.InvalidCurrency" });
        if (errors.Count > 0) return ResultDomain<Money>.Error(errors);
        return ResultDomain<Money>.Ok(new Money(amount, currency));
    }

    public ResultDomain<Money> Add(Money other)
    {
        if (other.Currency != Currency)
            return ResultDomain<Money>.Error(new MessageItem { Code = "Money.CurrencyMismatch" });
        return ResultDomain<Money>.Ok(new Money(Amount + other.Amount, Currency));
    }

    public ResultDomain<Money> Subtract(Money other)
    {
        if (other.Currency != Currency)
            return ResultDomain<Money>.Error(new MessageItem { Code = "Money.CurrencyMismatch" });
        if (Amount < other.Amount)
            return ResultDomain<Money>.Error(new MessageItem { Code = "Money.InsufficientAmount" });
        return ResultDomain<Money>.Ok(new Money(Amount - other.Amount, Currency));
    }

    public override string ToString() => $"{Amount} {Currency}";
}