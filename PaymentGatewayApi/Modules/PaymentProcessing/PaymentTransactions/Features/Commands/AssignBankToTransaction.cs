using PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.PaymentProcessing.PaymentTransactions.Features.Commands;

public static class AssignBankToTransaction
{
    public class AssignBankToTransactionCommand
    {
        public required Guid TransactionId { get; set; }
        public required Guid SelectedBankId { get; set; }
        public required string MerchantCode { get; set; }
        public required string TerminalCode { get; set; }
    }
    
    public class AssignBankToTransactionCommandResponse
    {
    }

    [Transactional]
    public class AssignBankToTransactionHandler
    {
        public async Task<FeatureObjectResultModel<AssignBankToTransactionCommandResponse>> Handle(
            AssignBankToTransactionCommand cmd,
            PaymentProcessingContext db,
            CancellationToken ct)
        {
            var transaction = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(x => x.Id == cmd.TransactionId, ct);
            if (transaction is null)
                return FeatureObjectResultModel<AssignBankToTransactionCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(PaymentTransaction),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = transaction.AssignBank(cmd.SelectedBankId, cmd.MerchantCode, cmd.TerminalCode);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AssignBankToTransactionCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<AssignBankToTransactionCommandResponse>.Ok(new AssignBankToTransactionCommandResponse());
        }
    }
}