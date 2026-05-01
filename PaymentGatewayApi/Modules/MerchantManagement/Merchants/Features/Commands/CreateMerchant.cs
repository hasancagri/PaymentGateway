using PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Commands;

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
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchantResult = Merchant.Create(cmd.Name, cmd.Email, cmd.Phone, cmd.Country, cmd.City, cmd.Mcc);
            if (!merchantResult.IsSuccess)
                return FeatureObjectResultModel<CreateMerchantResponse>.Error(merchantResult.Messages!);

            await db.Set<Merchant>().AddAsync(merchantResult.Data!, ct);
            return FeatureObjectResultModel<CreateMerchantResponse>.Ok(new CreateMerchantResponse { Id = merchantResult.Data!.Id });
        }
    }
}