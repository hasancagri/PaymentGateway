using PaymentGatewayApi.Modules.Settlement.Settlements.Enums;
using Settlement.Api.Settlement.MerchantBalances.Entities;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances;

public sealed class MerchantBalance : AggregateRoot
{
    public Guid MerchantId { get; init; }

    private Money _balance;
    public Money Balance { get => _balance; init => _balance = value; }

    [Newtonsoft.Json.JsonProperty]
    private List<BalanceMovement> _movements = [];
    [Newtonsoft.Json.JsonProperty]
    private List<WithdrawalRequest> _withdrawals = [];

    public IReadOnlyCollection<BalanceMovement>   Movements   => _movements.AsReadOnly();
    public IReadOnlyCollection<WithdrawalRequest> Withdrawals => _withdrawals.AsReadOnly();

    [Newtonsoft.Json.JsonConstructor]
    private MerchantBalance() { }

    public static MerchantBalance Create(Guid merchantId, string currency)
    {
        return new MerchantBalance
        {
            MerchantId = merchantId,
            Balance    = Money.Zero(currency),
        };
    }

    public ResultDomain Credit(Money amount, string description, Guid? referenceId = null)
    {
        if (amount.Currency != Balance.Currency)
            return ResultDomain.Error(new MessageItem { Code = "MerchantBalance.CurrencyMismatch" });
        if (amount.Amount <= 0)
            return ResultDomain.Error(new MessageItem { Code = "MerchantBalance.InvalidCreditAmount" });

        _balance = _balance.Add(amount).Data!;
        _movements.Add(BalanceMovement.Create(BalanceMovementType.Credit, amount, description, referenceId));
        return ResultDomain.Ok();
    }

    public ResultDomain Debit(Money amount, string description, Guid? referenceId = null)
    {
        if (amount.Currency != Balance.Currency)
            return ResultDomain.Error(new MessageItem { Code = "MerchantBalance.CurrencyMismatch" });
        if (amount.Amount <= 0)
            return ResultDomain.Error(new MessageItem { Code = "MerchantBalance.InvalidDebitAmount" });

        var subtractResult = Balance.Subtract(amount);
        if (!subtractResult.IsSuccess) return ResultDomain.Error(subtractResult.Messages!);

        _balance = subtractResult.Data!;
        _movements.Add(BalanceMovement.Create(BalanceMovementType.Debit, amount, description, referenceId));
        return ResultDomain.Ok();
    }

    public ResultDomain<WithdrawalRequest> RequestWithdrawal(Money amount, string targetIban)
    {
        if (amount.Amount > Balance.Amount)
            return ResultDomain<WithdrawalRequest>.Error(new MessageItem { Code = "MerchantBalance.InsufficientBalance" });

        if (_withdrawals.Any(w => w.Status == WithdrawalStatus.Requested || w.Status == WithdrawalStatus.Approved))
            return ResultDomain<WithdrawalRequest>.Error(new MessageItem { Code = "MerchantBalance.PendingWithdrawalExists" });

        var result = WithdrawalRequest.Create(amount, targetIban);
        if (!result.IsSuccess) return ResultDomain<WithdrawalRequest>.Error(result.Messages!);

        _withdrawals.Add(result.Data!);
        return ResultDomain<WithdrawalRequest>.Ok(result.Data!);
    }

    public ResultDomain ApproveWithdrawal(Guid withdrawalId)
    {
        var getResult = GetWithdrawal(withdrawalId);
        if (!getResult.IsSuccess) return ResultDomain.Error(getResult.Messages!);
        return getResult.Data!.Approve();
    }

    public ResultDomain RejectWithdrawal(Guid withdrawalId, string reason)
    {
        var getResult = GetWithdrawal(withdrawalId);
        if (!getResult.IsSuccess) return ResultDomain.Error(getResult.Messages!);
        return getResult.Data!.Reject(reason);
    }

    public ResultDomain ProcessWithdrawal(Guid withdrawalId)
    {
        var getResult = GetWithdrawal(withdrawalId);
        if (!getResult.IsSuccess) return ResultDomain.Error(getResult.Messages!);

        var withdrawal  = getResult.Data!;
        var debitResult = Debit(withdrawal.Amount, $"Withdrawal processed to {withdrawal.TargetIban}", withdrawalId);
        if (!debitResult.IsSuccess) return debitResult;

        return withdrawal.MarkProcessed();
    }

    private ResultDomain<WithdrawalRequest> GetWithdrawal(Guid withdrawalId)
    {
        var withdrawal = _withdrawals.SingleOrDefault(w => w.Id == withdrawalId);
        if (withdrawal is null)
            return ResultDomain<WithdrawalRequest>.Error(new MessageItem { Code = "WithdrawalRequest.NotFound", Params = [withdrawalId.ToString()] });
        return ResultDomain<WithdrawalRequest>.Ok(withdrawal);
    }
}