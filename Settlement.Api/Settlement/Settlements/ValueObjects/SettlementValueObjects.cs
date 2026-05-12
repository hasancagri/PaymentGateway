namespace Settlement.Api.Settlement.Settlements.ValueObjects;

public sealed record SettlementPeriod
{
    public DateOnly Start { get; }
    public DateOnly End   { get; }

    [Newtonsoft.Json.JsonConstructor]
    private SettlementPeriod(DateOnly start, DateOnly end) { Start = start; End = end; }

    public static ResultDomain<SettlementPeriod> Create(DateOnly start, DateOnly end)
    {
        if (start > end)
            return ResultDomain<SettlementPeriod>.Error(new MessageItem { Code = "SettlementPeriod.StartAfterEnd" });
        return ResultDomain<SettlementPeriod>.Ok(new SettlementPeriod(start, end));
    }

    public bool Contains(DateOnly date) => date >= Start && date <= End;
}