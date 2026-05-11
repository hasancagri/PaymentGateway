namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

public static class ActivateMerchant
{
    public class ActivateMerchantCommand
    {
        public required Guid MerchantId { get; set; }
        public required string Reason { get; set; }
    }

    public class ActivateMerchantCommandResponse
    {
    }

    [Transactional]
    public class ActivateMerchantHandler
    {
        public async Task<(FeatureObjectResultModel<ActivateMerchantCommandResponse>, MerchantStatusChanged?)> Handle(
            ActivateMerchantCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return (FeatureObjectResultModel<ActivateMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }), null);

            var oldStatus = merchant.Status;
            var result = merchant.Activate(cmd.Reason);
            if (!result.IsSuccess)
                return (FeatureObjectResultModel<ActivateMerchantCommandResponse>.Error(result.Messages!), null);

            session.Store(merchant);

            var integrationEvent = new MerchantStatusChanged(
                merchant.Id,
                oldStatus.ToString(),
                merchant.Status.ToString(),
                DateTime.UtcNow);

            return (FeatureObjectResultModel<ActivateMerchantCommandResponse>.Ok(new ActivateMerchantCommandResponse()), integrationEvent);
        }
    }
}