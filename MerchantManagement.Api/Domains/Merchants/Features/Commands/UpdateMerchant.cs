namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

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
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<UpdateMerchantCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var updateResult = merchant.Update(cmd.Name, cmd.Email, cmd.Phone, cmd.Country, cmd.City, cmd.Mcc,
                cmd.WebhookUrl);
            if (!updateResult.IsSuccess)
                return FeatureObjectResultModel<UpdateMerchantCommandResponse>.Error(updateResult.Messages!);

            session.Store(merchant);

            await bus.PublishAsync(new MerchantSynced(
                MerchantId: merchant.Id,
                WebhookUrl: merchant.WebhookUrl.Value,
                IsActive: merchant.Status == MerchantStatus.Active,
                OccurredOn: DateTime.UtcNow));

            return FeatureObjectResultModel<UpdateMerchantCommandResponse>.Ok(new UpdateMerchantCommandResponse());
        }
    }
}