using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

public static class ApplyTransactionCommission
{
    public class ApplyTransactionCommissionCommand
    {
        public required Guid TransactionId { get; set; }
        public required decimal BankRate { get; set; }
        public required decimal MerchantRate { get; set; }
    }
    
    public class ApplyTransactionCommissionCommandResponse
    {
    }

    [Transactional]
    public class ApplyTransactionCommissionHandler
    {
        public async Task<FeatureObjectResultModel<ApplyTransactionCommissionCommandResponse>> Handle(
            ApplyTransactionCommissionCommand cmd,
            PaymentProcessingContext db,
            CancellationToken ct)
        {
            var transaction = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(x => x.Id == cmd.TransactionId, ct);
            if (transaction is null)
                return FeatureObjectResultModel<ApplyTransactionCommissionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(PaymentTransaction),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = transaction.ApplyCommission(cmd.BankRate, cmd.MerchantRate);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ApplyTransactionCommissionCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<ApplyTransactionCommissionCommandResponse>.Ok(new ApplyTransactionCommissionCommandResponse());
        }
    }
}