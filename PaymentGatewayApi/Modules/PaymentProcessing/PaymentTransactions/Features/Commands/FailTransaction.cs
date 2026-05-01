using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

public static class FailTransaction
{
    public class FailTransactionCommand
    {
        public required Guid TransactionId { get; set; }
        public required string Reason { get; set; }
    }
    
    public class FailTransactionCommandResponse
    {
    }

    [Transactional]
    public class FailTransactionHandler
    {
        public async Task<FeatureObjectResultModel<FailTransactionCommandResponse>> Handle(
            FailTransactionCommand cmd,
            PaymentProcessingContext db,
            CancellationToken ct)
        {
            var transaction = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(x => x.Id == cmd.TransactionId, ct);
            if (transaction is null)
                return FeatureObjectResultModel<FailTransactionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(PaymentTransaction),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = transaction.Fail(cmd.Reason);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<FailTransactionCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<FailTransactionCommandResponse>.Ok(new FailTransactionCommandResponse());
        }
    }
}