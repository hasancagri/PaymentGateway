namespace Settlement.Api.Settlement.Settlements.Features.Commands;

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
            IDocumentSession session,
            CancellationToken ct)
        {
            var settlement = await session.LoadAsync<Settlements.Settlement>(cmd.SettlementId, ct);
            if (settlement is null)
                return FeatureObjectResultModel<CompleteSettlementCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Settlements.Settlement),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = settlement.Complete();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CompleteSettlementCommandResponse>.Error(result.Messages!);

            session.Store(settlement);
            return FeatureObjectResultModel<CompleteSettlementCommandResponse>.Ok(new CompleteSettlementCommandResponse());
        }
    }
}