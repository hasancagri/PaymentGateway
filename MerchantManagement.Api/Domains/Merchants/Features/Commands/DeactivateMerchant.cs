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
        public async Task<FeatureObjectResultModel<DeactivateMerchantCommandResponse>> Handle(
            DeactivateMerchantCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.Deactivate(cmd.Reason);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Error(result.Messages!);

            session.Store(merchant);

            await bus.PublishAsync(new MerchantSynced(
                MerchantId: merchant.Id,
                WebhookUrl: merchant.WebhookUrl.Value,
                IsActive: false,
                OccurredOn: DateTime.UtcNow));

            return FeatureObjectResultModel<DeactivateMerchantCommandResponse>.Ok(new DeactivateMerchantCommandResponse());
        }
    }
}