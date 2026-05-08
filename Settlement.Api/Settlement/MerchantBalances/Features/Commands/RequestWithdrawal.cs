using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Commands;

public static class RequestWithdrawal
{
    public class RequestWithdrawalCommand
    {
        public required Guid MerchantId { get; set; }
        public required decimal Amount { get; set; }
        public required string Currency { get; set; }
        public required string TargetIban { get; set; }
    }

    public class RequestWithdrawalResponse
    {
        public Guid WithdrawalId { get; set; }
    }

    [Transactional]
    public class RequestWithdrawalHandler
    {
        public async Task<FeatureObjectResultModel<RequestWithdrawalResponse>> Handle(
            RequestWithdrawalCommand cmd,
            SettlementContext db,
            CancellationToken ct)
        {
            var balance = await db.Set<MerchantBalance>()
                .Include(x => x.Withdrawals)
                .FirstOrDefaultAsync(x => x.MerchantId == cmd.MerchantId, ct);

            if (balance is null)
                return FeatureObjectResultModel<RequestWithdrawalResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantBalance),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var moneyResult = Money.Create(cmd.Amount, cmd.Currency);
            if (!moneyResult.IsSuccess)
                return FeatureObjectResultModel<RequestWithdrawalResponse>.Error(moneyResult.Messages!);

            var result = balance.RequestWithdrawal(moneyResult.Data!, cmd.TargetIban);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RequestWithdrawalResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<RequestWithdrawalResponse>.Ok(new RequestWithdrawalResponse { WithdrawalId = result.Data!.Id });
        }
    }
}