namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

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
        public async Task<(FeatureObjectResultModel<SuspendMerchantCommandResponse>, MerchantStatusChanged?)> Handle(
            SuspendMerchantCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
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

            session.Store(merchant);

            var integrationEvent = new MerchantStatusChanged(
                merchant.Id,
                oldStatus.ToString(),
                merchant.Status.ToString(),
                DateTime.UtcNow);

            return (FeatureObjectResultModel<SuspendMerchantCommandResponse>.Ok(new SuspendMerchantCommandResponse()), integrationEvent);
        }
    }
}