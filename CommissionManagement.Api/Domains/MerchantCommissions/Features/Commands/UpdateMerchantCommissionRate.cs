using CommissionManagement.Api.Domains.BankCommissions.ValueObjects;
using CommissionManagement.Api.Domains.MerchantCommissions;

namespace CommissionManagement.Api.CommissionManagement.MerchantCommissions.Features.Commands;

public static class UpdateMerchantCommissionRate
{
    public class UpdateMerchantCommissionRateCommand
    {
        public required Guid CommissionId { get; set; }
        public required decimal NewRate { get; set; }
    }

    public class UpdateMerchantCommissionRateCommandResponse
    {
    }

    [Transactional]
    public class UpdateMerchantCommissionRateHandler
    {
        public async Task<FeatureObjectResultModel<UpdateMerchantCommissionRateCommandResponse>> Handle(
            UpdateMerchantCommissionRateCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var commission = await session.LoadAsync<MerchantCommission>(cmd.CommissionId, ct);
            if (commission is null)
                return FeatureObjectResultModel<UpdateMerchantCommissionRateCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantCommission),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var bankCommission = await session.LoadAsync<BankCommission>(commission.BankCommissionId, ct);
            if (bankCommission is null)
                return FeatureObjectResultModel<UpdateMerchantCommissionRateCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(BankCommission),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var rateResult = CommissionRate.Create(cmd.NewRate);
            if (!rateResult.IsSuccess)
                return FeatureObjectResultModel<UpdateMerchantCommissionRateCommandResponse>.Error(rateResult.Messages!);

            var updateResult = commission.UpdateRate(rateResult.Data!, bankCommission.Rate);
            if (!updateResult.IsSuccess)
                return FeatureObjectResultModel<UpdateMerchantCommissionRateCommandResponse>.Error(updateResult.Messages!);

            session.Store(commission);

            await bus.PublishAsync(new MerchantCommissionRateUpdated(commission.Id, cmd.NewRate, DateTime.UtcNow));
            return FeatureObjectResultModel<UpdateMerchantCommissionRateCommandResponse>.Ok(new UpdateMerchantCommissionRateCommandResponse());
        }
    }
}