using PaymentGatewayApi.Modules.Settlement.Settlements.Entities;
using PaymentGatewayApi.Modules.Settlement.Settlements.Enums;
using PaymentGatewayApi.Modules.Settlement.Settlements.Events;
using PaymentGatewayApi.Modules.Settlement.Settlements.ValueObjects;

namespace PaymentGatewayApi.Modules.Settlement.Settlements;

public sealed class Settlement : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public SettlementId Id { get; private set; }
    public Guid MerchantId { get; private set; } // Cross-BC reference
    public SettlementPeriod Period { get; private set; }
    public SettlementStatus Status { get; private set; }
    public string Currency { get; private set; }

    // ── Collections ───────────────────────────────────────
    private readonly List<SettlementLine> _lines = [];
    public IReadOnlyCollection<SettlementLine> Lines => _lines.AsReadOnly();

    // ── Computed ──────────────────────────────────────────
    public Money TotalGrossAmount => SumLines(l => l.GrossAmount);
    public Money TotalCommissionAmount => SumLines(l => l.CommissionAmount);
    public Money TotalNetAmount => SumLines(l => l.NetAmount);

    // ── Audit ─────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private Settlement()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static Settlement Start(
        Guid merchantId,
        SettlementPeriod period,
        string currency)
    {
        var settlement = new Settlement
        {
            Id = SettlementId.New(),
            MerchantId = merchantId,
            Period = period,
            Status = SettlementStatus.Pending,
            Currency = currency.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        settlement.RaiseDomainEvent(new SettlementStarted(
            Guid.NewGuid(), DateTime.UtcNow,
            settlement.Id.Value,
            merchantId,
            period.Start.ToString("yyyy-MM-dd"),
            period.End.ToString("yyyy-MM-dd")));

        return settlement;
    }

    // ── Processing ────────────────────────────────────────
    public void MarkProcessing()
    {
        if (Status != SettlementStatus.Pending)
            throw new DomainException("Only pending settlements can be moved to processing.");

        Status = SettlementStatus.Processing;
        Touch();
    }

    public void AddLine(
        Guid transactionId,
        Money grossAmount,
        Money commissionAmount,
        Money netAmount)
    {
        if (Status != SettlementStatus.Processing)
            throw new DomainException("Lines can only be added while settlement is processing.");

        if (grossAmount.Currency != Currency)
            throw new DomainException(
                $"Line currency '{grossAmount.Currency}' does not match settlement currency '{Currency}'.");

        var line = SettlementLine.Create(transactionId, grossAmount, commissionAmount, netAmount);
        _lines.Add(line);
        Touch();

        RaiseDomainEvent(new SettlementLineAdded(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, transactionId,
            netAmount.Amount, netAmount.Currency));
    }

    public void Complete()
    {
        if (Status != SettlementStatus.Processing)
            throw new DomainException("Only processing settlements can be completed.");
        if (!_lines.Any())
            throw new DomainException("Cannot complete a settlement with no lines.");

        Status = SettlementStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Touch();

        RaiseDomainEvent(new SettlementCompleted(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value,
            MerchantId,
            TotalNetAmount.Amount,
            Currency));
    }

    public void Fail(string reason)
    {
        if (Status == SettlementStatus.Completed)
            throw new DomainException("A completed settlement cannot be failed.");

        Status = SettlementStatus.Failed;
        Touch();

        RaiseDomainEvent(new SettlementFailed(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, reason));
    }

    // ── Helpers ───────────────────────────────────────────
    private Money SumLines(Func<SettlementLine, Money> selector)
    {
        if (!_lines.Any())
            return new Money(0, Currency);

        return _lines
            .Select(selector)
            .Aggregate((a, b) => a.Add(b));
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}