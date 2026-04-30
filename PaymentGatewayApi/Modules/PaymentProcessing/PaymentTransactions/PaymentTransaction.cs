using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Enums;
using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Events;
using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions;

public sealed class PaymentTransaction : AggregateRoot
{
    // ── Identity ──────────────────────────────────────────
    public TransactionId     Id            { get; private set; }
    public Guid              MerchantId    { get; private set; } // Cross-BC reference
    public OrderId           OrderId       { get; private set; }
    public TransactionType   Type          { get; private set; }
    public TransactionStatus Status        { get; private set; }

    // ── Financial ─────────────────────────────────────────
    public Money           Amount         { get; private set; }
    public CommissionInfo? CommissionInfo { get; private set; }

    // ── Card ──────────────────────────────────────────────
    public CardInfo CardInfo { get; private set; }

    // ── Routing ───────────────────────────────────────────
    public BankRoutingInfo? RoutingInfo { get; private set; }

    // ── Bank Response ─────────────────────────────────────
    public string? BankResponseCode  { get; private set; }
    public string? BankMessage       { get; private set; }
    public string? BankTransactionId { get; private set; }

    // ── Settlement ────────────────────────────────────────
    public bool      IsSettled    { get; private set; }
    public DateTime? SettledAt    { get; private set; }
    public Guid?     SettlementId { get; private set; } // Cross-BC reference

    // ── Audit ─────────────────────────────────────────────
    public DateTime  CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PaymentTransaction() { } // EF Core

    // ── Factory ───────────────────────────────────────────
    public static PaymentTransaction Initiate(
        Guid            merchantId,
        OrderId         orderId,
        TransactionType type,
        Money           amount,
        CardInfo        cardInfo)
    {
        var transaction = new PaymentTransaction
        {
            Id         = TransactionId.New(),
            MerchantId = merchantId,
            OrderId    = orderId,
            Type       = type,
            Status     = TransactionStatus.Pending,
            Amount     = amount,
            CardInfo   = cardInfo,
            CreatedAt  = DateTime.UtcNow
        };

        transaction.RaiseDomainEvent(new PaymentInitiated(
            Guid.NewGuid(), DateTime.UtcNow,
            transaction.Id.Value,
            merchantId,
            orderId.Value,
            amount.Amount,
            amount.Currency));

        return transaction;
    }

    // ── Bank Routing ──────────────────────────────────────
    public void AssignBank(BankRoutingInfo routingInfo)
    {
        EnsureStatus(TransactionStatus.Pending, "assign bank");
        RoutingInfo = routingInfo;
        Touch();

        RaiseDomainEvent(new BankSelected(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, routingInfo.SelectedBankId));
    }

    // ── Commission ────────────────────────────────────────
    public void ApplyCommission(CommissionInfo commissionInfo)
    {
        EnsureStatus(TransactionStatus.Pending, "apply commission");
        CommissionInfo = commissionInfo;
        Touch();
    }

    // ── Terminal States ───────────────────────────────────
    public void Approve(
        string bankTransactionId,
        string bankResponseCode,
        string bankMessage)
    {
        EnsureStatus(TransactionStatus.Pending, "approve");

        if (CommissionInfo is null)
            throw new DomainException("Commission must be applied before approving.");

        Status            = TransactionStatus.Approved;
        BankTransactionId = bankTransactionId;
        BankResponseCode  = bankResponseCode;
        BankMessage       = bankMessage;
        Touch();

        RaiseDomainEvent(new PaymentApproved(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value,
            MerchantId,
            OrderId.Value,
            Amount.Amount,
            Amount.Currency,
            CommissionInfo.MerchantAmount,
            CommissionInfo.BankAmount,
            CommissionInfo.NetAmount));
    }

    public void Decline(string bankResponseCode, string bankMessage)
    {
        EnsureStatus(TransactionStatus.Pending, "decline");

        Status           = TransactionStatus.Declined;
        BankResponseCode = bankResponseCode;
        BankMessage      = bankMessage;
        Touch();

        RaiseDomainEvent(new PaymentDeclined(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, OrderId.Value, bankResponseCode, bankMessage));
    }

    public void Fail(string reason)
    {
        if (Status != TransactionStatus.Pending)
            throw new DomainException("Only pending transactions can be marked as failed.");

        Status = TransactionStatus.Failed;
        Touch();

        RaiseDomainEvent(new PaymentFailed(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, reason));
    }

    // ── Settlement ────────────────────────────────────────
    public void MarkAsSettled(Guid settlementId)
    {
        if (Status != TransactionStatus.Approved)
            throw new DomainException("Only approved transactions can be settled.");
        if (IsSettled)
            throw new DomainException("Transaction is already settled.");

        IsSettled    = true;
        SettledAt    = DateTime.UtcNow;
        SettlementId = settlementId;
        Touch();

        RaiseDomainEvent(new TransactionSettled(
            Guid.NewGuid(), DateTime.UtcNow,
            Id.Value, MerchantId, settlementId));
    }

    // ── Helpers ───────────────────────────────────────────
    private void EnsureStatus(TransactionStatus expected, string operation)
    {
        if (Status != expected)
            throw new DomainException(
                $"Cannot {operation}: transaction is in '{Status}' state, expected '{expected}'.");
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}