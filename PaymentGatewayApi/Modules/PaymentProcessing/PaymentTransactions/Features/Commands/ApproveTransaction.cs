using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

public static class ApproveTransaction
{
    public class ApproveTransactionCommand
    {
        public required Guid TransactionId { get; set; }
        public required string BankTransactionId { get; set; }
        public required string BankResponseCode { get; set; }
        public required string BankMessage { get; set; }
    }
    
    public class ApproveTransactionCommandResponse
    {
    }

    [Transactional]
    public class ApproveTransactionHandler
    {
        public async Task<FeatureObjectResultModel<ApproveTransactionCommandResponse>> Handle(
            ApproveTransactionCommand cmd,
            PaymentProcessingContext db,
            CancellationToken ct)
        {
            var transaction = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(x => x.Id == cmd.TransactionId, ct);
            if (transaction is null)
                return FeatureObjectResultModel<ApproveTransactionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(PaymentTransaction),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = transaction.Approve(cmd.BankTransactionId, cmd.BankResponseCode, cmd.BankMessage);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ApproveTransactionCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<ApproveTransactionCommandResponse>.Ok(new ApproveTransactionCommandResponse());
        }
    }
}