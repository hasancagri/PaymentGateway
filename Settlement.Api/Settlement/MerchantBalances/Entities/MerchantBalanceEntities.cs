using Common;
using Common.Domains;
using PaymentGatewayApi.Modules.Settlement.Settlements.Enums;

namespace Settlement.Api.Settlement.MerchantBalances.Entities;

public sealed class BalanceMovement : BaseModel
{
    public BalanceMovementType Type { get; init; }
    public Money Amount { get; init; }
    public string Description { get; init; }
    public Guid? ReferenceId { get; init; }
    public DateTime OccurredAt { get; init; }

    [Newtonsoft.Json.JsonConstructor]
    private BalanceMovement() { }

    internal static BalanceMovement Create(
        BalanceMovementType type, Money amount, string description, Guid? referenceId = null) => new()
    {
        Type = type,
        Amount = amount,
        Description = description,
        ReferenceId = referenceId,
        OccurredAt = DateTime.UtcNow
    };
}

public sealed class WithdrawalRequest : BaseModel
{
    public Money Amount { get; init; }
    public string TargetIban { get; init; }
    public DateTime RequestedAt { get; init; }

    private WithdrawalStatus _status;
    public WithdrawalStatus Status { get => _status; init => _status = value; }

    private string? _rejectionReason;
    public string? RejectionReason { get => _rejectionReason; init => _rejectionReason = value; }

    private DateTime? _processedAt;
    public DateTime? ProcessedAt { get => _processedAt; init => _processedAt = value; }

    [Newtonsoft.Json.JsonConstructor]
    private WithdrawalRequest() { }

    internal static ResultDomain<WithdrawalRequest> Create(Money amount, string targetIban)
    {
        if (string.IsNullOrWhiteSpace(targetIban))
            return ResultDomain<WithdrawalRequest>.Error(new MessageItem { Code = "WithdrawalRequest.IbanEmpty" });

        return ResultDomain<WithdrawalRequest>.Ok(new WithdrawalRequest
        {
            Amount = amount,
            TargetIban = targetIban.Trim().ToUpperInvariant(),
            Status = WithdrawalStatus.Requested,
            RequestedAt = DateTime.UtcNow
        });
    }

    internal ResultDomain Approve()
    {
        if (_status != WithdrawalStatus.Requested)
            return ResultDomain.Error(new MessageItem { Code = "WithdrawalRequest.CannotApprove" });
        _status = WithdrawalStatus.Approved;
        return ResultDomain.Ok();
    }

    internal ResultDomain Reject(string reason)
    {
        if (_status != WithdrawalStatus.Requested)
            return ResultDomain.Error(new MessageItem { Code = "WithdrawalRequest.CannotReject" });
        _status = WithdrawalStatus.Rejected;
        _rejectionReason = reason;
        _processedAt = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    internal ResultDomain MarkProcessed()
    {
        if (_status != WithdrawalStatus.Approved)
            return ResultDomain.Error(new MessageItem { Code = "WithdrawalRequest.CannotProcess" });
        _status = WithdrawalStatus.Processed;
        _processedAt = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}