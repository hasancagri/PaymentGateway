namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Commands;

public static class DebitMerchantBalance
{
    public class DebitMerchantBalanceCommand
    {
        public required Guid MerchantId { get; set; }
        public required decimal Amount { get; set; }
        public required string Currency { get; set; }
        public required string Description { get; set; }
        public Guid? ReferenceId { get; set; }
    }

    public class DebitMerchantBalanceCommandResponse
    {
    }

    [Transactional]
    public class DebitMerchantBalanceHandler
    {
        public async Task<FeatureObjectResultModel<DebitMerchantBalanceCommandResponse>> Handle(
            DebitMerchantBalanceCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var balance = await session.Query<MerchantBalance>()
                .Where(x => x.MerchantId == cmd.MerchantId)
                .FirstOrDefaultAsync(ct);

            if (balance is null)
                return FeatureObjectResultModel<DebitMerchantBalanceCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantBalance),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var moneyResult = Money.Create(cmd.Amount, cmd.Currency);
            if (!moneyResult.IsSuccess)
                return FeatureObjectResultModel<DebitMerchantBalanceCommandResponse>.Error(moneyResult.Messages!);

            var result = balance.Debit(moneyResult.Data!, cmd.Description, cmd.ReferenceId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<DebitMerchantBalanceCommandResponse>.Error(result.Messages!);

            session.Store(balance);
            return FeatureObjectResultModel<DebitMerchantBalanceCommandResponse>.Ok(new DebitMerchantBalanceCommandResponse());
        }
    }
}