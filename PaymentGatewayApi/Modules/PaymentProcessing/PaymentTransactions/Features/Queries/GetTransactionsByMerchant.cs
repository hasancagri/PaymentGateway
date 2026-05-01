using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Enums;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Queries;

public static class GetTransactionsByMerchant
{
    public class GetTransactionsByMerchantQuery
    {
        public required Guid MerchantId { get; set; }
    }

    public class TransactionListItem
    {
        public Guid Id { get; set; }
        public string OrderId { get; set; }
        public TransactionType Type { get; set; }
        public TransactionStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public bool IsSettled { get; set; }
    }

    public class GetTransactionsByMerchantHandler
    {
        public async Task<FeatureObjectResultModel<List<TransactionListItem>>> Handle(
            GetTransactionsByMerchantQuery query,
            PaymentProcessingContext db,
            CancellationToken ct)
        {
            var list = await db.Set<PaymentTransaction>()
                .Where(x => x.MerchantId == query.MerchantId)
                .Select(x => new TransactionListItem
                {
                    Id = x.Id,
                    OrderId = x.OrderId.Value,
                    Type = x.Type,
                    Status = x.Status,
                    Amount = x.Amount.Amount,
                    Currency = x.Amount.Currency,
                    IsSettled = x.IsSettled
                }).ToListAsync(ct);

            return FeatureObjectResultModel<List<TransactionListItem>>.Ok(list);
        }
    }
}