namespace Settlement.Api.Settlement.MerchantBalances.Features.Commands;

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
            IDocumentSession session,
            CancellationToken ct)
        {
            var balance = await session.Query<MerchantBalance>()
                .Where(x => x.MerchantId == cmd.MerchantId)
                .FirstOrDefaultAsync(ct);

            if (balance is null)
                return FeatureObjectResultModel<ApproveWithdrawalCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantBalance),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = balance.ApproveWithdrawal(cmd.WithdrawalId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ApproveWithdrawalCommandResponse>.Error(result.Messages!);

            session.Store(balance);
            return FeatureObjectResultModel<ApproveWithdrawalCommandResponse>.Ok(new ApproveWithdrawalCommandResponse());
        }
    }
}