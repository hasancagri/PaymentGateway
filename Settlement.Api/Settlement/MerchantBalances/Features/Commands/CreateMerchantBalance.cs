using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Commands;

public static class CreateMerchantBalance
{
    public class CreateMerchantBalanceCommand
    {
        public required Guid MerchantId { get; set; }
        public required string Currency { get; set; }
    }

    public class CreateMerchantBalanceResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateMerchantBalanceHandler
    {
        public async Task<FeatureObjectResultModel<CreateMerchantBalanceResponse>> Handle(
            CreateMerchantBalanceCommand cmd,
            SettlementContext db,
            CancellationToken ct)
        {
            var balance = MerchantBalance.Create(cmd.MerchantId, cmd.Currency);
            await db.Set<MerchantBalance>().AddAsync(balance, ct);
            return FeatureObjectResultModel<CreateMerchantBalanceResponse>.Ok(new CreateMerchantBalanceResponse { Id = balance.Id });
        }
    }
}