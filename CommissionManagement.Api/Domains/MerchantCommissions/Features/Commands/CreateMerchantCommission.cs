using CommissionManagement.Api.Domains.BankCommissions;

namespace CommissionManagement.Api.Domains.MerchantCommissions.Features.Commands;

public static class CreateMerchantCommission
{
    public class CreateMerchantCommissionCommand
    {
        public required Guid MerchantId { get; set; }
        public required Guid BankCommissionId { get; set; }
        public required CardBrand CardBrand { get; set; }
        public required CardType CardType { get; set; }
        public required TransactionRegion TransactionRegion { get; set; }
        public required decimal MerchantRate { get; set; }
    }

    public class CreateMerchantCommissionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class DefineMerchantCommissionHandler
    {
        public async Task<FeatureObjectResultModel<CreateMerchantCommissionResponse>> Handle(
            CreateMerchantCommissionCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var bankCommission = await session.LoadAsync<BankCommission>(cmd.BankCommissionId, ct);
            if (bankCommission is null)
                return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Error(new MessageItem
                {
                    Table = nameof(BankCommission),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var rateResult = CommissionRate.Create(cmd.MerchantRate);
            if (!rateResult.IsSuccess)
                return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Error(rateResult.Messages!);

            var criteria     = new CommissionCriteria(cmd.CardBrand, cmd.CardType, cmd.TransactionRegion);
            var defineResult = MerchantCommission.Create(
                cmd.MerchantId,
                cmd.BankCommissionId,
                criteria,
                rateResult.Data!,
                bankCommission.Rate);

            if (!defineResult.IsSuccess)
                return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Error(defineResult.Messages!);

            session.Store(defineResult.Data!);
            return FeatureObjectResultModel<CreateMerchantCommissionResponse>.Ok(new CreateMerchantCommissionResponse { Id = defineResult.Data.Id });
        }
    }
}