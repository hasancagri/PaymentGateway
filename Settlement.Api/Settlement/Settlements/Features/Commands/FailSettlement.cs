namespace Settlement.Api.Settlement.Settlements.Features.Commands;

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
            IDocumentSession session,
            CancellationToken ct)
        {
            var settlement = await session.LoadAsync<Settlements.Settlement>(cmd.SettlementId, ct);
            if (settlement is null)
                return FeatureObjectResultModel<FailSettlementCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Settlements.Settlement),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = settlement.Fail(cmd.Reason);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<FailSettlementCommandResponse>.Error(result.Messages!);

            session.Store(settlement);
            return FeatureObjectResultModel<FailSettlementCommandResponse>.Ok(new FailSettlementCommandResponse());
        }
    }
}