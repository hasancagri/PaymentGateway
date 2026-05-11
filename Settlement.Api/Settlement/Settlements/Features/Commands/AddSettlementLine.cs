namespace Settlement.Api.Settlement.Settlements.Features.Commands;

public static class AddSettlementLine
{
    public class AddSettlementLineCommand
    {
        public required Guid SettlementId { get; set; }
        public required Guid TransactionId { get; set; }
        public required decimal GrossAmount { get; set; }
        public required decimal CommissionAmount { get; set; }
        public required decimal NetAmount { get; set; }
        public required string Currency { get; set; }
    }

    public class AddSettlementLineCommandResponse
    {
    }

    [Transactional]
    public class AddSettlementLineHandler
    {
        public async Task<FeatureObjectResultModel<AddSettlementLineCommandResponse>> Handle(
            AddSettlementLineCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var settlement = await session.LoadAsync<Settlements.Settlement>(cmd.SettlementId, ct);
            if (settlement is null)
                return FeatureObjectResultModel<AddSettlementLineCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Settlements.Settlement),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var grossResult      = Money.Create(cmd.GrossAmount, cmd.Currency);
            var commissionResult = Money.Create(cmd.CommissionAmount, cmd.Currency);
            var netResult        = Money.Create(cmd.NetAmount, cmd.Currency);

            var errors = new List<MessageItem>();
            if (!grossResult.IsSuccess)      errors.AddRange(grossResult.Messages!);
            if (!commissionResult.IsSuccess) errors.AddRange(commissionResult.Messages!);
            if (!netResult.IsSuccess)        errors.AddRange(netResult.Messages!);
            if (errors.Count > 0)
                return FeatureObjectResultModel<AddSettlementLineCommandResponse>.Error(errors);

            var result = settlement.AddLine(cmd.TransactionId, grossResult.Data!, commissionResult.Data!, netResult.Data!);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddSettlementLineCommandResponse>.Error(result.Messages!);

            session.Store(settlement);
            return FeatureObjectResultModel<AddSettlementLineCommandResponse>.Ok(new AddSettlementLineCommandResponse());
        }
    }
}