namespace Payment.Api.Domains.Payments;

public class Payment : AggregateRoot
{
    private Payment()
    {
    }

    public string OrderNumber { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public int InstallmentCount { get; private set; }
    public PaymentStatus Status { get; private set; }

    /// <summary>Çekimin yapıldığı (veya 3D için yönlendirilen) banka kodu.</summary>
    public string? BankCode { get; private set; }

    /// <summary>Banka tarafındaki işlem numarası; iptal/iade için zorunludur.</summary>
    public string? TransactionId { get; private set; }

    public decimal RefundedAmount { get; private set; }
    public string? FailReason { get; private set; }

    [JsonProperty("Attempts")] private List<PaymentAttempt> _attempts = new();

    [JsonIgnore] public IReadOnlyList<PaymentAttempt> Attempts => _attempts.AsReadOnly();

    public static ResultDomain<Payment> Create(string orderNumber, decimal amount, int installmentCount)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(OrderNumber),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED
            });
        }

        if (amount <= 0)
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(Amount),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        if (installmentCount < 1)
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(InstallmentCount),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        return ResultDomain<Payment>.Ok(new Payment
        {
            OrderNumber = orderNumber,
            Amount = amount,
            InstallmentCount = installmentCount,
            Status = PaymentStatus.Pending
        });
    }

    /// <summary>Failover'daki her banka denemesi (başarılı/başarısız) kayda geçer.</summary>
    public void RecordAttempt(string bankCode, bool isSuccess, string? message)
    {
        _attempts.Add(new PaymentAttempt(bankCode, isSuccess, message, DateTime.UtcNow));
    }

    public ResultDomain MarkAwaiting3D(string bankCode)
    {
        if (Status != PaymentStatus.Pending)
            return InvalidStatus();

        Status = PaymentStatus.Awaiting3D;
        BankCode = bankCode;
        return ResultDomain.Ok();
    }

    public ResultDomain MarkCompleted(string bankCode, string? transactionId)
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Awaiting3D)
            return InvalidStatus();

        Status = PaymentStatus.Completed;
        BankCode = bankCode;
        TransactionId = transactionId;
        return ResultDomain.Ok();
    }

    public ResultDomain MarkFailed(string? reason)
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Awaiting3D)
            return InvalidStatus();

        Status = PaymentStatus.Failed;
        FailReason = reason;
        return ResultDomain.Ok();
    }

    /// <summary>İptal: yalnız tamamlanmış ve hiç iade görmemiş ödeme iptal edilebilir (tam tutar döner).</summary>
    public ResultDomain Cancel()
    {
        if (Status != PaymentStatus.Completed || RefundedAmount > 0)
            return InvalidStatus();

        Status = PaymentStatus.Cancelled;
        return ResultDomain.Ok();
    }

    /// <summary>Kısmi iade: toplam iade, çekilen tutarı aşamaz.</summary>
    public ResultDomain Refund(decimal refundAmount)
    {
        if (Status != PaymentStatus.Completed && Status != PaymentStatus.Refunded)
            return InvalidStatus();

        if (refundAmount <= 0 || RefundedAmount + refundAmount > Amount)
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(RefundedAmount),
                Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE
            });
        }

        RefundedAmount += refundAmount;
        Status = PaymentStatus.Refunded;
        return ResultDomain.Ok();
    }

    private static ResultDomain InvalidStatus()
    {
        return ResultDomain.Error(new MessageItem
        {
            Property = nameof(Status),
            Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR
        });
    }
}

/// <summary>Aggregate'e ait sade entity; bağımsız yaşamaz (BasketItem deseni).</summary>
public class PaymentAttempt
{
    public PaymentAttempt(string bankCode, bool isSuccess, string? message, DateTime attemptedTime)
    {
        BankCode = bankCode;
        IsSuccess = isSuccess;
        Message = message;
        AttemptedTime = attemptedTime;
    }

    public string BankCode { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? Message { get; private set; }
    public DateTime AttemptedTime { get; private set; }
}

public enum PaymentStatus
{
    Pending = 1,
    Awaiting3D = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Refunded = 6
}