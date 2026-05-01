using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

public static class DeclineTransaction
{
    public class DeclineTransactionCommand
    {
        public required Guid TransactionId { get; set; }
        public required string BankResponseCode { get; set; }
        public required string BankMessage { get; set; }
    }
    
    public class DeclineTransactionCommandResponse
    {
    }

    [Transactional]
    public class DeclineTransactionHandler
    {
        public async Task<FeatureObjectResultModel<DeclineTransactionCommandResponse>> Handle(
            DeclineTransactionCommand cmd,
            PaymentProcessingContext db,
            CancellationToken ct)
        {
            var transaction = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(x => x.Id == cmd.TransactionId, ct);
            if (transaction is null)
                return FeatureObjectResultModel<DeclineTransactionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(PaymentTransaction),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = transaction.Decline(cmd.BankResponseCode, cmd.BankMessage);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<DeclineTransactionCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<DeclineTransactionCommandResponse>.Ok(new DeclineTransactionCommandResponse());
        }
    }
}