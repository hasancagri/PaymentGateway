using PaymentGatewayApi.Modules.Settlement.Settlements.Enums;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Entities;

public sealed class BalanceMovement : BaseModel
{
    public BalanceMovementType Type        { get; private set; }
    public Money               Amount      { get; private set; }
    public string              Description { get; private set; }
    public Guid?               ReferenceId { get; private set; }
    public DateTime            OccurredAt   { get; private set; }

    private BalanceMovement() { }

    internal static BalanceMovement Create(
        BalanceMovementType type, Money amount, string description, Guid? referenceId = null) => new()
    {
        Type        = type,
        Amount      = amount,
        Description = description,
        ReferenceId = referenceId,
        OccurredAt   = DateTime.UtcNow
    };
}

public sealed class WithdrawalRequest : BaseModel
{
    public Money    Amount          { get; private set; }
    public string   TargetIban      { get; private set; }
    public WithdrawalStatus Status  { get; private set; }
    public string?  RejectionReason { get; private set; }
    public DateTime RequestedAt     { get; private set; }
    public DateTime? ProcessedAt    { get; private set; }

    private WithdrawalRequest() { }

    internal static ResultDomain<WithdrawalRequest> Create(Money amount, string targetIban)
    {
        if (string.IsNullOrWhiteSpace(targetIban))
            return ResultDomain<WithdrawalRequest>.Error(new MessageItem { Code = "WithdrawalRequest.IbanEmpty" });

        return ResultDomain<WithdrawalRequest>.Ok(new WithdrawalRequest
        {
            Amount      = amount,
            TargetIban  = targetIban.Trim().ToUpperInvariant(),
            Status      = WithdrawalStatus.Requested,
            RequestedAt = DateTime.UtcNow
        });
    }

    internal ResultDomain Approve()
    {
        if (Status != WithdrawalStatus.Requested)
            return ResultDomain.Error(new MessageItem { Code = "WithdrawalRequest.CannotApprove" });
        Status = WithdrawalStatus.Approved;
        return ResultDomain.Ok();
    }

    internal ResultDomain Reject(string reason)
    {
        if (Status != WithdrawalStatus.Requested)
            return ResultDomain.Error(new MessageItem { Code = "WithdrawalRequest.CannotReject" });
        Status          = WithdrawalStatus.Rejected;
        RejectionReason = reason;
        ProcessedAt     = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    internal ResultDomain MarkProcessed()
    {
        if (Status != WithdrawalStatus.Approved)
            return ResultDomain.Error(new MessageItem { Code = "WithdrawalRequest.CannotProcess" });
        Status      = WithdrawalStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}