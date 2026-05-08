using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.Settlement.Settlements.Features.Commands;

public static class MarkSettlementProcessing
{
    public class MarkSettlementProcessingCommand
    {
        public required Guid SettlementId { get; set; }
    }
    
    public class MarkSettlementProcessingCommandResponse
    {
    }

    [Transactional]
    public class MarkSettlementProcessingHandler
    {
        public async Task<FeatureObjectResultModel<MarkSettlementProcessingCommandResponse>> Handle(
            MarkSettlementProcessingCommand cmd,
            SettlementContext db,
            CancellationToken ct)
        {
            var settlement = await db.Set<Settlement>().FirstOrDefaultAsync(x => x.Id == cmd.SettlementId, ct);
            if (settlement is null)
                return FeatureObjectResultModel<MarkSettlementProcessingCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Settlement),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = settlement.MarkProcessing();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<MarkSettlementProcessingCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<MarkSettlementProcessingCommandResponse>.Ok(new MarkSettlementProcessingCommandResponse());
        }
    }
}