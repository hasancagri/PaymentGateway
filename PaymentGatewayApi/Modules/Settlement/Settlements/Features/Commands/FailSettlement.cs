using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.Settlement.Settlements.Features.Commands;

public static class FailSettlement
{
    public class FailSettlementCommand
    {
        public required Guid SettlementId { get; set; }
        public required string Reason { get; set; }
    }
    
    public class FailSettlementCommandResponse
    {
    }

    [Transactional]
    public class FailSettlementHandler
    {
        public async Task<FeatureObjectResultModel<FailSettlementCommandResponse>> Handle(
            FailSettlementCommand cmd,
            SettlementContext db,
            CancellationToken ct)
        {
            var settlement = await db.Set<Settlement>().FirstOrDefaultAsync(x => x.Id == cmd.SettlementId, ct);
            if (settlement is null)
                return FeatureObjectResultModel<FailSettlementCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Settlement),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = settlement.Fail(cmd.Reason);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<FailSettlementCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<FailSettlementCommandResponse>.Ok(new FailSettlementCommandResponse());
        }
    }
}