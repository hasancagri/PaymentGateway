using PaymentGatewayApi.Modules.Settlement.MerchantBalances.Entities;
using PaymentGatewayApi.Modules.Settlement.MerchantBalances.Events;
using PaymentGatewayApi.Modules.Settlement.MerchantBalances.ValueObjects;
using PaymentGatewayApi.Modules.Settlement.Settlements.Enums;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances;

public sealed class MerchantBalance : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public MerchantBalanceId Id { get; private set; }
    public Guid MerchantId { get; private set; } // Cross-BC reference
    public Money Balance { get; private set; }

    // ── Collections ───────────────────────────────────────
    private readonly List<BalanceMovement> _movements = [];
    private readonly List<WithdrawalRequest> _withdrawals = [];

    public IReadOnlyCollection<BalanceMovement> Movements => _movements.AsReadOnly();
    public IReadOnlyCollection<WithdrawalRequest> Withdrawals => _withdrawals.AsReadOnly();

    // ── Audit ─────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private MerchantBalance()
    {
    } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static MerchantBalance Create(Guid merchantId, string currency)
    {
        return new MerchantBalance
        {
            Id = MerchantBalanceId.New(),
            MerchantId = merchantId,
            Balance = new Money(0, currency),
            CreatedAt = DateTime.UtcNow
        };
    }

    // ── Credit ────────────────────────────────────────────
    public void Credit(Money amount, string description, Guid? referenceId = null)
    {
        if (amount.Currency != Balance.Currency)
            throw new DomainException($"Cannot credit '{amount.Currency}' to a '{Balance.Currency}' balance.");
        if (amount.Amount <= 0)
            throw new DomainException("Credit amount must be greater than zero.");

        Balance = Balance.Add(amount);
        Touch();

        _movements.Add(BalanceMovement.Create(
            BalanceMovementType.Credit, amount, description, referenceId));

        RaiseDomainEvent(new MerchantBalanceCredited(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, MerchantId,
            amount.Amount, amount.Currency, referenceId));
    }

    // ── Debit ─────────────────────────────────────────────
    public void Debit(Money amount, string description, Guid? referenceId = null)
    {
        if (amount.Currency != Balance.Currency)
            throw new DomainException($"Cannot debit '{amount.Currency}' from a '{Balance.Currency}' balance.");
        if (amount.Amount <= 0)
            throw new DomainException("Debit amount must be greater than zero.");
        if (Balance.Amount < amount.Amount)
            throw new DomainException("Insufficient balance.");

        Balance = Balance.Subtract(amount);
        Touch();

        _movements.Add(BalanceMovement.Create(
            BalanceMovementType.Debit, amount, description, referenceId));

        RaiseDomainEvent(new MerchantBalanceDebited(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, MerchantId,
            amount.Amount, amount.Currency, referenceId));
    }

    // ── Withdrawal ────────────────────────────────────────
    public WithdrawalRequest RequestWithdrawal(Money amount, string targetIban)
    {
        if (amount.Amount > Balance.Amount)
            throw new DomainException("Withdrawal amount exceeds available balance.");

        if (_withdrawals.Any(w =>
                w.Status == WithdrawalStatus.Requested ||
                w.Status == WithdrawalStatus.Approved))
        {
            throw new DomainException("A pending withdrawal already exists. Wait for it to be processed.");
        }

        var withdrawal = WithdrawalRequest.Create(amount, targetIban);
        _withdrawals.Add(withdrawal);
        Touch();

        RaiseDomainEvent(new WithdrawalRequested(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, MerchantId,
            withdrawal.Id.Value,
            amount.Amount, amount.Currency, targetIban));

        return withdrawal;
    }

    public void ApproveWithdrawal(Guid withdrawalId)
    {
        var withdrawal = GetWithdrawal(withdrawalId);
        withdrawal.Approve();
        Touch();

        RaiseDomainEvent(new WithdrawalApproved(
            Guid.NewGuid(), DateTime.UtcNow,
            MerchantId, withdrawalId));
    }

    public void RejectWithdrawal(Guid withdrawalId, string reason)
    {
        var withdrawal = GetWithdrawal(withdrawalId);
        withdrawal.Reject(reason);
        Touch();

        RaiseDomainEvent(new WithdrawalRejected(
            Guid.NewGuid(), DateTime.UtcNow,
            MerchantId, withdrawalId, reason));
    }

    public void ProcessWithdrawal(Guid withdrawalId)
    {
        var withdrawal = GetWithdrawal(withdrawalId);

        Debit(withdrawal.Amount,
            $"Withdrawal processed to {withdrawal.TargetIban}",
            withdrawalId);

        withdrawal.MarkProcessed();
        Touch();

        RaiseDomainEvent(new WithdrawalProcessed(
            Guid.NewGuid(), DateTime.UtcNow,
            MerchantId, withdrawalId));
    }

    // ── Helpers ───────────────────────────────────────────
    private WithdrawalRequest GetWithdrawal(Guid withdrawalId) =>
        _withdrawals.SingleOrDefault(w => w.Id.Value == withdrawalId)
        ?? throw new DomainException($"Withdrawal '{withdrawalId}' not found.");

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}