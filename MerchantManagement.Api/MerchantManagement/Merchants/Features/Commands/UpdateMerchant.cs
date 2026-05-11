using MerchantManagement.Api.Modules.MerchantManagement.Merchants.ValueObjects;
using Wolverine.Attributes;
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Commands;

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
        public async Task<(FeatureObjectResultModel<UpdateMerchantCommandResponse>, SharedMerchantEvents.MerchantUpdated?)> Handle(
            UpdateMerchantCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>().FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);
            if (merchant is null)
                return (FeatureObjectResultModel<UpdateMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }), null);

            var updateResult = merchant.Update(cmd.Name, cmd.Email, cmd.Phone, cmd.Country, cmd.City, cmd.Mcc, cmd.WebhookUrl);
            if (!updateResult.IsSuccess)
                return (FeatureObjectResultModel<UpdateMerchantCommandResponse>.Error(updateResult.Messages!), null);

            var integrationEvent = new SharedMerchantEvents.MerchantUpdated(
                merchant.Id,
                cmd.Name,
                cmd.WebhookUrl,
                DateTime.UtcNow);

            return (FeatureObjectResultModel<UpdateMerchantCommandResponse>.Ok(new UpdateMerchantCommandResponse()), integrationEvent);
        }
    }
}