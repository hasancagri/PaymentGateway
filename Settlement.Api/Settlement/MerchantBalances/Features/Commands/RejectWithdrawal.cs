using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Commands;

public static class RejectWithdrawal
{
    public class RejectWithdrawalCommand
    {
        public required Guid MerchantId { get; set; }
        public required Guid WithdrawalId { get; set; }
        public required string Reason { get; set; }
    }
    
    public class RejectWithdrawalCommandResponse
    {
    }

    [Transactional]
    public class RejectWithdrawalHandler
    {
        public async Task<FeatureObjectResultModel<RejectWithdrawalCommandResponse>> Handle(
            RejectWithdrawalCommand cmd,
            SettlementContext db,
            CancellationToken ct)
        {
            var balance = await db.Set<MerchantBalance>()
                .Include(x => x.Withdrawals)
                .FirstOrDefaultAsync(x => x.MerchantId == cmd.MerchantId, ct);

            if (balance is null)
                return FeatureObjectResultModel<RejectWithdrawalCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantBalance),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = balance.RejectWithdrawal(cmd.WithdrawalId, cmd.Reason);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RejectWithdrawalCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<RejectWithdrawalCommandResponse>.Ok(new RejectWithdrawalCommandResponse());
        }
    }
}