namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

public static class CreateMerchant
{
    public class CreateMerchantCommand
    {
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Phone { get; set; }
        public required string Country { get; set; }
        public required string City { get; set; }
        public required string Mcc { get; set; }
        public required string WebhookUrl { get; set; }
    }

    public class CreateMerchantResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateMerchantHandler
    {
        public async Task<FeatureObjectResultModel<CreateMerchantResponse>> Handle(
            CreateMerchantCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var merchantResult = Merchant.Create(cmd.Name, cmd.Email, cmd.Phone, cmd.Country, cmd.City, cmd.Mcc, cmd.WebhookUrl);
            if (!merchantResult.IsSuccess)
                return FeatureObjectResultModel<CreateMerchantResponse>.Error(merchantResult.Messages!);

            var merchant = merchantResult.Data!;
            session.Store(merchant);

            await bus.PublishAsync(new MerchantSynced(
                MerchantId: merchant.Id,
                WebhookUrl: merchant.WebhookUrl.Value,
                IsActive: true,
                OccurredOn: DateTime.UtcNow));

            return FeatureObjectResultModel<CreateMerchantResponse>.Ok(new CreateMerchantResponse { Id = merchant.Id });
        }
    }
}