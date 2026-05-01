using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Features.Commands;

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
            CommissionManagementContext db,
            CancellationToken ct)
        {
            var commission = await db.Set<BankCommission>().FirstOrDefaultAsync(x => x.Id == cmd.CommissionId, ct);
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
            return FeatureObjectResultModel<UpdateBankCommissionRateCommandResponse>.Ok(new UpdateBankCommissionRateCommandResponse());
        }
    }
}