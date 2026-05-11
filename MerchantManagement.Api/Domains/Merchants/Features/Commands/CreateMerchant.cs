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
        public async Task<(FeatureObjectResultModel<CreateMerchantResponse>, MerchantCreated?)> Handle(
            CreateMerchantCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchantResult = Merchant.Create(cmd.Name, cmd.Email, cmd.Phone, cmd.Country, cmd.City, cmd.Mcc, cmd.WebhookUrl);
            if (!merchantResult.IsSuccess)
                return (FeatureObjectResultModel<CreateMerchantResponse>.Error(merchantResult.Messages!), null);

            var merchant = merchantResult.Data!;
            session.Store(merchant);

            var integrationEvent = new MerchantCreated(
                merchant.Id,
                merchant.Name.Value,
                merchant.ContactInfo.Email,
                merchant.Address.Country,
                merchant.WebhookUrl.Value,
                DateTime.UtcNow);

            return (FeatureObjectResultModel<CreateMerchantResponse>.Ok(new CreateMerchantResponse { Id = merchant.Id }), integrationEvent);
        }
    }
}