namespace CommissionManagement.Api.Domains.BankCommissions.Features.Commands;

public static class UpdateBankCommissionRate
{
    public class UpdateBankCommissionRateCommand
    {
        public required Guid CommissionId { get; set; }
        public required decimal NewRate { get; set; }
    }

    public class UpdateBankCommissionRateCommandResponse
    {
    }

    [Transactional]
    public class UpdateBankCommissionRateHandler
    {
        public async Task<FeatureObjectResultModel<UpdateBankCommissionRateCommandResponse>> Handle(
            UpdateBankCommissionRateCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var commission = await session.LoadAsync<BankCommission>(cmd.CommissionId, ct);
            if (commission is null)
                return FeatureObjectResultModel<UpdateBankCommissionRateCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(BankCommission),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var rateResult = CommissionRate.Create(cmd.NewRate);
            if (!rateResult.IsSuccess)
                return FeatureObjectResultModel<UpdateBankCommissionRateCommandResponse>.Error(rateResult.Messages!);

            commission.UpdateRate(rateResult.Data!);
            session.Store(commission);

            await bus.PublishAsync(new BankCommissionSynced(
                commission.Id,
                commission.BankId,
                commission.Criteria.CardBrand,
                commission.Criteria.CardType,
                commission.Criteria.TransactionRegion,
                commission.Rate.Value,
                DateTime.UtcNow));

            return FeatureObjectResultModel<UpdateBankCommissionRateCommandResponse>.Ok(new UpdateBankCommissionRateCommandResponse());
        }
    }
}