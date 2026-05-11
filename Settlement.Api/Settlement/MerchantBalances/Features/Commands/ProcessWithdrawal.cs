namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Commands;

public static class ProcessWithdrawal
{
    public class ProcessWithdrawalCommand
    {
        public required Guid MerchantId { get; set; }
        public required Guid WithdrawalId { get; set; }
    }

    public class ProcessWithdrawalCommandResponse
    {
    }

    [Transactional]
    public class ProcessWithdrawalHandler
    {
        public async Task<FeatureObjectResultModel<ProcessWithdrawalCommandResponse>> Handle(
            ProcessWithdrawalCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var balance = await session.Query<MerchantBalance>()
                .Where(x => x.MerchantId == cmd.MerchantId)
                .FirstOrDefaultAsync(ct);

            if (balance is null)
                return FeatureObjectResultModel<ProcessWithdrawalCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantBalance),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = balance.ProcessWithdrawal(cmd.WithdrawalId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ProcessWithdrawalCommandResponse>.Error(result.Messages!);

            session.Store(balance);
            return FeatureObjectResultModel<ProcessWithdrawalCommandResponse>.Ok(new ProcessWithdrawalCommandResponse());
        }
    }
}