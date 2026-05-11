namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

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
        public async Task<(FeatureObjectResultModel<DeactivateMerchantCommandResponse>, MerchantStatusChanged?)> Handle(
            DeactivateMerchantCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
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

            session.Store(merchant);

            var integrationEvent = new MerchantStatusChanged(
                merchant.Id,
                oldStatus.ToString(),
                merchant.Status.ToString(),
                DateTime.UtcNow);

            return (FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Ok(new DeactivateMerchantCommandResponse()), integrationEvent);
        }
    }
}