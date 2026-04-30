namespace PaymentGatewayApi.Modules.Settlement.Settlements.ValueObjects;

public sealed record SettlementId(Guid Value)
{
    public static SettlementId New()            => new(Guid.NewGuid());
    public static SettlementId From(Guid value) => new(value);
    public override string ToString()           => Value.ToString();
}

public sealed record SettlementPeriod
{
    public DateOnly Start { get; }
    public DateOnly End   { get; }

    public SettlementPeriod(DateOnly start, DateOnly end)
    {
        if (start > end)
            throw new DomainException("Settlement period start cannot be after end.");

        Start = start;
        End   = end;
    }

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public override string ToString() => $"{Start:yyyy-MM-dd} / {End:yyyy-MM-dd}";
}

