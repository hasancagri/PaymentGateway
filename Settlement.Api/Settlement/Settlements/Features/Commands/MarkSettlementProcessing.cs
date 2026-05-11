namespace Settlement.Api.Settlement.Settlements.Features.Commands;

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
            IDocumentSession session,
            CancellationToken ct)
        {
            var settlement = await session.LoadAsync<Settlements.Settlement>(cmd.SettlementId, ct);
            if (settlement is null)
                return FeatureObjectResultModel<MarkSettlementProcessingCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Settlements.Settlement),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = settlement.MarkProcessing();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<MarkSettlementProcessingCommandResponse>.Error(result.Messages!);

            session.Store(settlement);
            return FeatureObjectResultModel<MarkSettlementProcessingCommandResponse>.Ok(new MarkSettlementProcessingCommandResponse());
        }
    }
}