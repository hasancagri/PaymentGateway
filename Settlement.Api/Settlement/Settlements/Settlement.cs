using PaymentGatewayApi.Modules.Settlement.Settlements.Enums;
using Settlement.Api.Settlement.Settlements.Entities;
using Settlement.Api.Settlement.Settlements.ValueObjects;
using Settlement.Api.Shared;

namespace Settlement.Api.Settlement.Settlements;

public sealed class Settlement : AggregateRoot
{
    public Guid MerchantId { get; init; }
    public SettlementPeriod Period { get; init; }
    public string Currency { get; init; }

    private SettlementStatus _status;

    public SettlementStatus Status
    {
        get => _status;
        init => _status = value;
    }

    private DateTime? _completedAt;

    public DateTime? CompletedAt
    {
        get => _completedAt;
        init => _completedAt = value;
    }

    [Newtonsoft.Json.JsonProperty]
    private List<SettlementLine> _lines = [];
    public IReadOnlyCollection<SettlementLine> Lines => _lines.AsReadOnly();

    public Money TotalGrossAmount => SumLines(l => l.GrossAmount);
    public Money TotalCommissionAmount => SumLines(l => l.CommissionAmount);
    public Money TotalNetAmount => SumLines(l => l.NetAmount);

    [Newtonsoft.Json.JsonConstructor]
    private Settlement()
    {
    }

    public static Settlement Start(Guid merchantId, SettlementPeriod period, string currency)
    {
        return new Settlement
        {
            MerchantId = merchantId,
            Period = period,
            Status = SettlementStatus.Pending,
            Currency = currency.ToUpperInvariant(),
        };
    }

    public ResultDomain MarkProcessing()
    {
        if (_status != SettlementStatus.Pending)
            return ResultDomain.Error(new MessageItem { Code = "Settlement.CannotMarkProcessing" });
        _status = SettlementStatus.Processing;
        return ResultDomain.Ok();
    }

    public ResultDomain AddLine(Guid transactionId, Money grossAmount, Money commissionAmount, Money netAmount)
    {
        if (_status != SettlementStatus.Processing)
            return ResultDomain.Error(new MessageItem { Code = "Settlement.NotProcessing" });
        if (grossAmount.Currency != Currency)
            return ResultDomain.Error(new MessageItem
                { Code = "Settlement.CurrencyMismatch", Params = [grossAmount.Currency, Currency] });

        _lines.Add(SettlementLine.Create(transactionId, grossAmount, commissionAmount, netAmount));
        return ResultDomain.Ok();
    }

    public ResultDomain Complete()
    {
        if (_status != SettlementStatus.Processing)
            return ResultDomain.Error(new MessageItem { Code = "Settlement.CannotComplete" });
        if (!_lines.Any())
            return ResultDomain.Error(new MessageItem { Code = "Settlement.NoLines" });

        _status = SettlementStatus.Completed;
        _completedAt = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    public ResultDomain Fail(string reason)
    {
        if (_status == SettlementStatus.Completed)
            return ResultDomain.Error(new MessageItem { Code = "Settlement.AlreadyCompleted" });
        _status = SettlementStatus.Failed;
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