using PaymentGatewayApi.Modules.Settlement.Settlements.Entities;
using PaymentGatewayApi.Modules.Settlement.Settlements.Enums;
using PaymentGatewayApi.Modules.Settlement.Settlements.ValueObjects;

namespace PaymentGatewayApi.Modules.Settlement.Settlements;

public sealed class Settlement : AggregateRoot
{
    public Guid             MerchantId  { get; private set; }
    public SettlementPeriod Period      { get; private set; }
    public SettlementStatus Status      { get; private set; }
    public string           Currency    { get; private set; }

    private readonly List<SettlementLine> _lines = [];
    public IReadOnlyCollection<SettlementLine> Lines => _lines.AsReadOnly();

    public Money TotalGrossAmount      => SumLines(l => l.GrossAmount);
    public Money TotalCommissionAmount => SumLines(l => l.CommissionAmount);
    public Money TotalNetAmount        => SumLines(l => l.NetAmount);

    public DateTime? CompletedAt { get; private set; }

    private Settlement() { }

    public static Settlement Start(Guid merchantId, SettlementPeriod period, string currency)
    {
        return new Settlement
        {
            MerchantId = merchantId,
            Period     = period,
            Status     = SettlementStatus.Pending,
            Currency   = currency.ToUpperInvariant(),
        };
    }

    public ResultDomain MarkProcessing()
    {
        if (Status != SettlementStatus.Pending)
            return ResultDomain.Error(new MessageItem { Code = "Settlement.CannotMarkProcessing" });
        Status = SettlementStatus.Processing;
        return ResultDomain.Ok();
    }

    public ResultDomain AddLine(Guid transactionId, Money grossAmount, Money commissionAmount, Money netAmount)
    {
        if (Status != SettlementStatus.Processing)
            return ResultDomain.Error(new MessageItem { Code = "Settlement.NotProcessing" });
        if (grossAmount.Currency != Currency)
            return ResultDomain.Error(new MessageItem { Code = "Settlement.CurrencyMismatch", Params = [grossAmount.Currency, Currency] });

        _lines.Add(SettlementLine.Create(transactionId, grossAmount, commissionAmount, netAmount));
        return ResultDomain.Ok();
    }

    public ResultDomain Complete()
    {
        if (Status != SettlementStatus.Processing)
            return ResultDomain.Error(new MessageItem { Code = "Settlement.CannotComplete" });
        if (!_lines.Any())
            return ResultDomain.Error(new MessageItem { Code = "Settlement.NoLines" });

        Status      = SettlementStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    public ResultDomain Fail(string reason)
    {
        if (Status == SettlementStatus.Completed)
            return ResultDomain.Error(new MessageItem { Code = "Settlement.AlreadyCompleted" });
        Status = SettlementStatus.Failed;
        return ResultDomain.Ok();
    }

    private Money SumLines(Func<SettlementLine, Money> selector)
    {
        if (!_lines.Any())
            return Money.Zero(Currency);

        var acc = Money.Zero(Currency);
        foreach (var line in _lines)
            acc = acc.Add(selector(line)).Data!;
        return acc;
    }
}