using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Features.Commands;

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
            CommissionManagementContext db,
            CancellationToken ct)
        {
            var commission = await db.Set<MerchantCommission>().FirstOrDefaultAsync(x => x.Id == cmd.CommissionId, ct);
            if (commission is null)
                return FeatureObjectResultModel<UpdateMerchantCommissionRateCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(MerchantCommission),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var bankCommission = await db.Set<BankCommission>().FirstOrDefaultAsync(x => x.Id == commission.BankCommissionId, ct);
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

            return FeatureObjectResultModel<UpdateMerchantCommissionRateCommandResponse>.Ok(new UpdateMerchantCommissionRateCommandResponse());
        }
    }
}