using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.Settlement.Settlements.Features.Commands;

public static class CompleteSettlement
{
    public class CompleteSettlementCommand
    {
        public required Guid SettlementId { get; set; }
    }
    
    public class CompleteSettlementCommandResponse  
    {
    }

    [Transactional]
    public class CompleteSettlementHandler
    {
        public async Task<FeatureObjectResultModel<CompleteSettlementCommandResponse>> Handle(
            CompleteSettlementCommand cmd,
            SettlementContext db,
            CancellationToken ct)
        {
            var settlement = await db.Set<Settlement>()
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == cmd.SettlementId, ct);

            if (settlement is null)
                return FeatureObjectResultModel<CompleteSettlementCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Settlement),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = settlement.Complete();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CompleteSettlementCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<CompleteSettlementCommandResponse>.Ok(new CompleteSettlementCommandResponse());
        }
    }
}