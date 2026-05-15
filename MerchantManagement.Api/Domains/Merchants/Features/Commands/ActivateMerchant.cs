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
        public async Task<FeatureObjectResultModel<ActivateMerchantCommandResponse>> Handle(
            ActivateMerchantCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<ActivateMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.Activate(cmd.Reason);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ActivateMerchantCommandResponse>.Error(result.Messages!);

            session.Store(merchant);

            await bus.PublishAsync(new MerchantSynced(
                MerchantId: merchant.Id,
                WebhookUrl: merchant.WebhookUrl.Value,
                IsActive: true,
                OccurredOn: DateTime.UtcNow));

            await bus.PublishAsync(new MerchantStatusChanged(
                MerchantId: merchant.Id,
                NewStatus: MerchantStatus.Active,
                ApiKeyHashes: merchant.ApiKeys.Where(k => k.IsActive()).Select(k => k.KeyValue.Hash).ToList(),
                OccurredOn: DateTime.UtcNow));

            return FeatureObjectResultModel<ActivateMerchantCommandResponse>.Ok(new ActivateMerchantCommandResponse());
        }
    }
}