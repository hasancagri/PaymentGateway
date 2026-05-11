using PaymentGateway.SharedContracts.CommissionEvents;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.Enums;
using PaymentGatewayApi.Modules.CommissionManagement.BankCommissions.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.CommissionManagement.MerchantCommissions.Features.Commands;

public static class DefineMerchantCommission
{
    public class DefineMerchantCommissionCommand
    {
        public required Guid MerchantId { get; set; }
        public required Guid BankCommissionId { get; set; }
        public required CardBrand CardBrand { get; set; }
        public required CardType CardType { get; set; }
        public required TransactionRegion TransactionRegion { get; set; }
        public required decimal MerchantRate { get; set; }
    }

    public class DefineMerchantCommissionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class DefineMerchantCommissionHandler
    {
        public async Task<FeatureObjectResultModel<DefineMerchantCommissionResponse>> Handle(
            DefineMerchantCommissionCommand cmd,
            CommissionManagementContext db,
            IMessageBus bus,
            CancellationToken ct)
        {
            var bankCommission = await db.Set<BankCommission>().FirstOrDefaultAsync(x => x.Id == cmd.BankCommissionId, ct);
            if (bankCommission is null)
                return FeatureObjectResultModel<DefineMerchantCommissionResponse>.Error(new MessageItem
                {
                    Table = nameof(BankCommission),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var rateResult = CommissionRate.Create(cmd.MerchantRate);
            if (!rateResult.IsSuccess)
                return FeatureObjectResultModel<DefineMerchantCommissionResponse>.Error(rateResult.Messages!);

            var criteria     = new CommissionCriteria(cmd.CardBrand, cmd.CardType, cmd.TransactionRegion);
            var defineResult = MerchantCommission.Define(
                cmd.MerchantId,
                cmd.BankCommissionId,
                criteria,
                rateResult.Data!,
                bankCommission.Rate);

            if (!defineResult.IsSuccess)
                return FeatureObjectResultModel<DefineMerchantCommissionResponse>.Error(defineResult.Messages!);

            await db.Set<MerchantCommission>().AddAsync(defineResult.Data!, ct);

            await bus.PublishAsync(new MerchantCommissionSynced(
                defineResult.Data!.Id,
                defineResult.Data.MerchantId,
                defineResult.Data.BankCommissionId,
                defineResult.Data.Rate.Value,
                DateTime.UtcNow));

            return FeatureObjectResultModel<DefineMerchantCommissionResponse>.Ok(new DefineMerchantCommissionResponse { Id = defineResult.Data.Id });
        }
    }
}