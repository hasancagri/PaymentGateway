using MerchantManagement.Api.Modules.MerchantManagement.Merchants.ValueObjects;
using Wolverine.Attributes;
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Commands;

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
        public async Task<(FeatureObjectResultModel<CreateMerchantResponse>, SharedMerchantEvents.MerchantCreated?)> Handle(
            CreateMerchantCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchantResult = Merchant.Create(cmd.Name, cmd.Email, cmd.Phone, cmd.Country, cmd.City, cmd.Mcc, cmd.WebhookUrl);
            if (!merchantResult.IsSuccess)
                return (FeatureObjectResultModel<CreateMerchantResponse>.Error(merchantResult.Messages!), null);

            var merchant = merchantResult.Data!;
            await db.Set<Merchant>().AddAsync(merchant, ct);

            var integrationEvent = new SharedMerchantEvents.MerchantCreated(
                merchant.Id,
                merchant.Name.Value,
                merchant.ContactInfo.Email,
                merchant.Address.Country,
                DateTime.UtcNow);

            return (FeatureObjectResultModel<CreateMerchantResponse>.Ok(new CreateMerchantResponse { Id = merchant.Id }), integrationEvent);
        }
    }
}