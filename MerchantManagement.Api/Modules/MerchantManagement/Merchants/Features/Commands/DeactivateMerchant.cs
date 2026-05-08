using Wolverine.Attributes;
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Commands;

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
        public async Task<(FeatureObjectResultModel<DeactivateMerchantCommandResponse>, SharedMerchantEvents.MerchantStatusChanged?)> Handle(
            DeactivateMerchantCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>().FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);
            if (merchant is null)
                return (FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }), null);

            var oldStatus = merchant.Status;
            var result = merchant.Deactivate(cmd.Reason);
            if (!result.IsSuccess)
                return (FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Error(result.Messages!), null);

            var integrationEvent = new SharedMerchantEvents.MerchantStatusChanged(
                merchant.Id,
                oldStatus.ToString(),
                merchant.Status.ToString(),
                DateTime.UtcNow);

            return (FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Ok(new DeactivateMerchantCommandResponse()), integrationEvent);
        }
    }
}