using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Commands;

public static class ApproveWithdrawal
{
    public class ApproveWithdrawalCommand
    {
        public required Guid MerchantId { get; set; }
        public required Guid WithdrawalId { get; set; }
    }
    
    public class ApproveWithdrawalCommandResponse
    {
    }

    [Transactional]
    public class ApproveWithdrawalHandler
    {
        public async Task<FeatureObjectResultModel<ApproveWithdrawalCommandResponse>> Handle(
            ApproveWithdrawalCommand cmd,
            SettlementContext db,
            CancellationToken ct)
        {
            var balance = await db.Set<MerchantBalance>()
                .Include(x => x.Withdrawals)
                .FirstOrDefaultAsync(x => x.MerchantId == cmd.MerchantId, ct);

            if (balance is null)
                return FeatureObjectResultModel<ApproveWithdrawalCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantBalance),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = balance.ApproveWithdrawal(cmd.WithdrawalId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ApproveWithdrawalCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<ApproveWithdrawalCommandResponse>.Ok(new ApproveWithdrawalCommandResponse());
        }
    }
}