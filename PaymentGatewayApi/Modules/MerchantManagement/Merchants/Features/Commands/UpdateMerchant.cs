using PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Commands;

public static class UpdateMerchant
{
    public class UpdateMerchantCommand
    {
        public required Guid MerchantId { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Phone { get; set; }
        public required string Country { get; set; }
        public required string City { get; set; }
        public required string Mcc { get; set; }
        public required string WebhookUrl { get; set; }
    }
    
    public class UpdateMerchantCommandResponse  
    {
    }

    [Transactional]
    public class UpdateMerchantHandler
    {
        public async Task<FeatureObjectResultModel<UpdateMerchantCommandResponse>> Handle(
            UpdateMerchantCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>().FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<UpdateMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var updateResult = merchant.Update(cmd.Name, cmd.Email, cmd.Phone, cmd.Country, cmd.City, cmd.Mcc, cmd.WebhookUrl);
            if (!updateResult.IsSuccess)
                return FeatureObjectResultModel<UpdateMerchantCommandResponse>.Error(updateResult.Messages!);

            return FeatureObjectResultModel<UpdateMerchantCommandResponse>.Ok(new UpdateMerchantCommandResponse());
        }
    }
}