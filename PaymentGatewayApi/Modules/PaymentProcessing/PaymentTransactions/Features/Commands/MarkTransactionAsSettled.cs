using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

public static class MarkTransactionAsSettled
{
    public class MarkTransactionAsSettledCommand
    {
        public required Guid TransactionId { get; set; }
        public required Guid SettlementId { get; set; }
    }
    
    public class MarkTransactionAsSettledCommandResponse
    {
    }

    [Transactional]
    public class MarkTransactionAsSettledHandler
    {
        public async Task<FeatureObjectResultModel<MarkTransactionAsSettledCommandResponse>> Handle(
            MarkTransactionAsSettledCommand cmd,
            PaymentProcessingContext db,
            CancellationToken ct)
        {
            var transaction = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(x => x.Id == cmd.TransactionId, ct);
            if (transaction is null)
                return FeatureObjectResultModel<MarkTransactionAsSettledCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(PaymentTransaction),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = transaction.MarkAsSettled(cmd.SettlementId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<MarkTransactionAsSettledCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<MarkTransactionAsSettledCommandResponse>.Ok(new MarkTransactionAsSettledCommandResponse());
        }
    }
}