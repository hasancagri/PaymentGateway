using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Enums;
using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions;

public sealed class PaymentTransaction : AggregateRoot
{
    public Guid MerchantId { get; private set; }
    public OrderId OrderId { get; private set; }
    public TransactionType Type { get; private set; }
    public TransactionStatus Status { get; private set; }
    public Money Amount { get; private set; }
    public CommissionInfo? CommissionInfo { get; private set; }
    public CardInfo CardInfo { get; private set; }
    public BankRoutingInfo? RoutingInfo { get; private set; }
    public string? BankResponseCode { get; private set; }
    public string? BankMessage { get; private set; }
    public string? BankTransactionId { get; private set; }
    public bool IsSettled { get; private set; }
    public DateTime? SettledAt { get; private set; }
    public Guid? SettlementId { get; private set; }

    private PaymentTransaction()
    {
    }

    public static ResultDomain<PaymentTransaction> Initiate(
        Guid merchantId, string orderId, TransactionType type,
        decimal amount, string currency,
        string encryptedCardNumber, string cardHolderName,
        string expiryMonth, string expiryYear, string cardHolderIp)
    {
        var orderIdResult = OrderId.Create(orderId);
        var moneyResult = Money.Create(amount, currency);
        var cardResult = CardInfo.Create(encryptedCardNumber, cardHolderName, expiryMonth, expiryYear, cardHolderIp);

        var errors = new List<MessageItem>();
        if (!orderIdResult.IsSuccess) errors.AddRange(orderIdResult.Messages!);
        if (!moneyResult.IsSuccess) errors.AddRange(moneyResult.Messages!);
        if (!cardResult.IsSuccess) errors.AddRange(cardResult.Messages!);
        if (errors.Count > 0) return ResultDomain<PaymentTransaction>.Error(errors);

        return ResultDomain<PaymentTransaction>.Ok(new PaymentTransaction
        {
            MerchantId = merchantId,
            OrderId = orderIdResult.Data!,
            Type = type,
            Status = TransactionStatus.Pending,
            Amount = moneyResult.Data!,
            CardInfo = cardResult.Data!,
        });
    }

    public ResultDomain AssignBank(Guid selectedBankId, string merchantCode, string terminalCode)
    {
        var statusCheck = EnsureStatus(TransactionStatus.Pending, "assign bank");
        if (!statusCheck.IsSuccess) return statusCheck;

        var routingResult = BankRoutingInfo.Create(selectedBankId, merchantCode, terminalCode);
        if (!routingResult.IsSuccess) return ResultDomain.Error(routingResult.Messages!);

        RoutingInfo = routingResult.Data!;
        return ResultDomain.Ok();
    }

    public ResultDomain ApplyCommission(decimal bankRate, decimal merchantRate)
    {
        var statusCheck = EnsureStatus(TransactionStatus.Pending, "apply commission");
        if (!statusCheck.IsSuccess) return statusCheck;

        var commissionResult = CommissionInfo.Create(Amount.Amount, bankRate, merchantRate);
        if (!commissionResult.IsSuccess) return ResultDomain.Error(commissionResult.Messages!);

        CommissionInfo = commissionResult.Data!;
        return ResultDomain.Ok();
    }

    public ResultDomain Approve(string bankTransactionId, string bankResponseCode, string bankMessage)
    {
        var statusCheck = EnsureStatus(TransactionStatus.Pending, "approve");
        if (!statusCheck.IsSuccess) return statusCheck;

        if (CommissionInfo is null)
            return ResultDomain.Error(new MessageItem { Code = "Transaction.CommissionRequired" });

        Status = TransactionStatus.Approved;
        BankTransactionId = bankTransactionId;
        BankResponseCode = bankResponseCode;
        BankMessage = bankMessage;
        return ResultDomain.Ok();
    }

    public ResultDomain Decline(string bankResponseCode, string bankMessage)
    {
        var statusCheck = EnsureStatus(TransactionStatus.Pending, "decline");
        if (!statusCheck.IsSuccess) return statusCheck;

        Status = TransactionStatus.Declined;
        BankResponseCode = bankResponseCode;
        BankMessage = bankMessage;
        return ResultDomain.Ok();
    }

    public ResultDomain Fail(string reason)
    {
        if (Status != TransactionStatus.Pending)
            return ResultDomain.Error(new MessageItem
                { Code = "Transaction.CannotFail", Params = [Status.ToString()] });

        Status = TransactionStatus.Failed;
        return ResultDomain.Ok();
    }

    public ResultDomain MarkAsSettled(Guid settlementId)
    {
        if (Status != TransactionStatus.Approved)
            return ResultDomain.Error(new MessageItem { Code = "Transaction.NotApproved" });
        if (IsSettled)
            return ResultDomain.Error(new MessageItem { Code = "Transaction.AlreadySettled" });

        IsSettled = true;
        SettledAt = DateTime.UtcNow;
        SettlementId = settlementId;
        return ResultDomain.Ok();
    }

    private ResultDomain EnsureStatus(TransactionStatus expected, string operation)
    {
        if (Status != expected)
            return ResultDomain.Error(new MessageItem
            {
                Code = "Transaction.InvalidState",
                Params = [operation, Status.ToString(), expected.ToString()]
            });
        return ResultDomain.Ok();
    }
}