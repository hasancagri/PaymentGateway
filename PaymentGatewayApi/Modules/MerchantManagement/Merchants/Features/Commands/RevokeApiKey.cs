using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Entities;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Commands;

public static class RevokeApiKey
{
    public class RevokeApiKeyCommand
    {
        public required Guid MerchantId { get; set; }
        public required Guid ApiKeyId { get; set; }
    }
    
    public class RevokeApiKeyCommandResponse
    {
    }

    [Transactional]
    public class RevokeApiKeyHandler
    {
        public async Task<FeatureObjectResultModel<RevokeApiKeyCommandResponse>> Handle(
            RevokeApiKeyCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>()
                .Include(x => x.ApiKeys)
                .FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);

            if (merchant is null)
                return FeatureObjectResultModel<RevokeApiKeyCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.RevokeApiKey(cmd.ApiKeyId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RevokeApiKeyCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<RevokeApiKeyCommandResponse>.Ok(new RevokeApiKeyCommandResponse());
        }
    }
}