namespace Common.Domains;

public sealed record Money
{
    public decimal Amount   { get; }
    public string  Currency { get; } // ISO 4217

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException("Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("Currency must be a 3-letter ISO 4217 code.");

        Amount   = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public Money Add(Money other)
    {
        if (other.Currency != Currency)
            throw new DomainException($"Cannot add '{other.Currency}' to '{Currency}'.");
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (other.Currency != Currency)
            throw new DomainException($"Cannot subtract '{other.Currency}' from '{Currency}'.");
        if (Amount < other.Amount)
            throw new DomainException("Insufficient amount.");
        return new Money(Amount - other.Amount, Currency);
    }

    public override string ToString() => $"{Amount} {Currency}";
}