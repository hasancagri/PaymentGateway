using Wolverine.Attributes;
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Commands;

public static class SuspendMerchant
{
    public class SuspendMerchantCommand
    {
        public required Guid MerchantId { get; set; }
        public required string Reason { get; set; }
    }

    public class SuspendMerchantCommandResponse
    {
    }

    [Transactional]
    public class SuspendMerchantHandler
    {
        public async Task<(FeatureObjectResultModel<SuspendMerchantCommandResponse>, SharedMerchantEvents.MerchantStatusChanged?)> Handle(
            SuspendMerchantCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>().FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);
            if (merchant is null)
                return (FeatureObjectResultModel<SuspendMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }), null);

            var oldStatus = merchant.Status;
            var result = merchant.Suspend(cmd.Reason);
            if (!result.IsSuccess)
                return (FeatureObjectResultModel<SuspendMerchantCommandResponse>.Error(result.Messages!), null);

            var integrationEvent = new SharedMerchantEvents.MerchantStatusChanged(
                merchant.Id,
                oldStatus.ToString(),
                merchant.Status.ToString(),
                DateTime.UtcNow);

            return (FeatureObjectResultModel<SuspendMerchantCommandResponse>.Ok(new SuspendMerchantCommandResponse()), integrationEvent);
        }
    }
}