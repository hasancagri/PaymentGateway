using PaymentGatewayApi.Modules.Settlement.MerchantBalances.ValueObjects;
using PaymentGatewayApi.Modules.Settlement.Settlements.Enums;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Entities;

public sealed class BalanceMovement
{
    public Guid Id { get; private set; }
    public BalanceMovementType Type { get; private set; }
    public Money Amount { get; private set; }
    public string Description { get; private set; }
    public Guid? ReferenceId { get; private set; } // SettlementId or WithdrawalId
    public DateTime OccuredAt { get; private set; }

    private BalanceMovement()
    {
    } // EF Core

    internal static BalanceMovement Create(
        BalanceMovementType type,
        Money amount,
        string description,
        Guid? referenceId = null) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Amount = amount,
        Description = description,
        ReferenceId = referenceId,
        OccuredAt = DateTime.UtcNow
    };
}

public sealed class WithdrawalRequest
{
    public WithdrawalId Id { get; private set; }
    public Money Amount { get; private set; }
    public string TargetIban { get; private set; }
    public WithdrawalStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private WithdrawalRequest()
    {
    } // EF Core

    internal static WithdrawalRequest Create(Money amount, string targetIban)
    {
        if (string.IsNullOrWhiteSpace(targetIban))
            throw new DomainException("Target IBAN cannot be empty.");

        return new WithdrawalRequest
        {
            Id = WithdrawalId.New(),
            Amount = amount,
            TargetIban = targetIban.Trim().ToUpperInvariant(),
            Status = WithdrawalStatus.Requested,
            RequestedAt = DateTime.UtcNow
        };
    }

    internal void Approve()
    {
        if (Status != WithdrawalStatus.Requested)
            throw new DomainException("Only requested withdrawals can be approved.");

        Status = WithdrawalStatus.Approved;
    }

    internal void Reject(string reason)
    {
        if (Status != WithdrawalStatus.Requested)
            throw new DomainException("Only requested withdrawals can be rejected.");

        Status = WithdrawalStatus.Rejected;
        RejectionReason = reason;
        ProcessedAt = DateTime.UtcNow;
    }

    internal void MarkProcessed()
    {
        if (Status != WithdrawalStatus.Approved)
            throw new DomainException("Only approved withdrawals can be marked as processed.");

        Status = WithdrawalStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
    }
}