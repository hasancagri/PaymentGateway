namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Queries;

public static class GetMerchantBalance
{
    public class GetMerchantBalanceQuery
    {
        public required Guid MerchantId { get; set; }
    }

    public class GetMerchantBalanceResponse
    {
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; }
    }

    public class GetMerchantBalanceHandler
    {
        public async Task<FeatureObjectResultModel<GetMerchantBalanceResponse>> Handle(
            GetMerchantBalanceQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var balance = await session.Query<MerchantBalance>()
                .Where(x => x.MerchantId == query.MerchantId)
                .FirstOrDefaultAsync(ct);

            if (balance is null)
                return FeatureObjectResultModel<GetMerchantBalanceResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantBalance),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetMerchantBalanceResponse>.Ok(new GetMerchantBalanceResponse
            {
                Id = balance.Id,
                MerchantId = balance.MerchantId,
                Balance = balance.Balance.Amount,
                Currency = balance.Balance.Currency
            });
        }
    }
}