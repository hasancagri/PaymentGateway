using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Enums;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Queries;

public static class GetTransactionById
{
    public class GetTransactionByIdQuery
    {
        public required Guid TransactionId { get; set; }
    }

    public class GetTransactionByIdResponse
    {
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public string OrderId { get; set; }
        public TransactionType Type { get; set; }
        public TransactionStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public bool IsSettled { get; set; }
        public string? BankResponseCode { get; set; }
        public string? BankMessage { get; set; }
        public string? BankTransactionId { get; set; }
    }

    public class GetTransactionByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetTransactionByIdResponse>> Handle(
            GetTransactionByIdQuery query,
            PaymentProcessingContext db,
            CancellationToken ct)
        {
            var transaction = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(x => x.Id == query.TransactionId, ct);
            if (transaction is null)
                return FeatureObjectResultModel<GetTransactionByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(PaymentTransaction),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetTransactionByIdResponse>.Ok(new GetTransactionByIdResponse
            {
                Id = transaction.Id,
                MerchantId = transaction.MerchantId,
                OrderId = transaction.OrderId.Value,
                Type = transaction.Type,
                Status = transaction.Status,
                Amount = transaction.Amount.Amount,
                Currency = transaction.Amount.Currency,
                IsSettled = transaction.IsSettled,
                BankResponseCode = transaction.BankResponseCode,
                BankMessage = transaction.BankMessage,
                BankTransactionId = transaction.BankTransactionId
            });
        }
    }
}