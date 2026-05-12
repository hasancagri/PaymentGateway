using Settlement.Api.Shared;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Commands;

public static class CreditMerchantBalance
{
    public class CreditMerchantBalanceCommand
    {
        public required Guid MerchantId { get; set; }
        public required decimal Amount { get; set; }
        public required string Currency { get; set; }
        public required string Description { get; set; }
        public Guid? ReferenceId { get; set; }
    }

    public class CreditMerchantBalanceCommandResponse
    {
    }

    [Transactional]
    public class CreditMerchantBalanceHandler
    {
        public async Task<FeatureObjectResultModel<CreditMerchantBalanceCommandResponse>> Handle(
            CreditMerchantBalanceCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var balance = await session.Query<MerchantBalance>()
                .Where(x => x.MerchantId == cmd.MerchantId)
                .FirstOrDefaultAsync(ct);

            if (balance is null)
                return FeatureObjectResultModel<CreditMerchantBalanceCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantBalance),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var moneyResult = Money.Create(cmd.Amount, cmd.Currency);
            if (!moneyResult.IsSuccess)
                return FeatureObjectResultModel<CreditMerchantBalanceCommandResponse>.Error(moneyResult.Messages!);

            var result = balance.Credit(moneyResult.Data!, cmd.Description, cmd.ReferenceId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreditMerchantBalanceCommandResponse>.Error(result.Messages!);

            session.Store(balance);
            return FeatureObjectResultModel<CreditMerchantBalanceCommandResponse>.Ok(new CreditMerchantBalanceCommandResponse());
        }
    }
}