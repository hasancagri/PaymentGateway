using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Commands;

public static class DeactivateMerchant
{
    public class DeactivateMerchantCommand
    {
        public required Guid MerchantId { get; set; }
        public required string Reason { get; set; }
    }
    
    public class DeactivateMerchantCommandResponse 
    {
    }

    [Transactional]
    public class DeactivateMerchantHandler
    {
        public async Task<FeatureObjectResultModel<DeactivateMerchantCommandResponse>> Handle(
            DeactivateMerchantCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>().FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.Deactivate(cmd.Reason);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Ok(new DeactivateMerchantCommandResponse());
        }
    }
}