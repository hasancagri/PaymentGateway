using MerchantManagement.Api.Modules.MerchantManagement.Merchants.Entities;
using Wolverine.Attributes;
using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Commands;

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
        public async Task<(FeatureObjectResultModel<RevokeApiKeyCommandResponse>, SharedMerchantEvents.ApiKeyRevoked?)> Handle(
            RevokeApiKeyCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>()
                .Include(x => x.ApiKeys)
                .FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);

            if (merchant is null)
                return (FeatureObjectResultModel<RevokeApiKeyCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }), null);

            var keyToRevoke = merchant.ApiKeys.SingleOrDefault(k => k.Id == cmd.ApiKeyId);
            if (keyToRevoke is null)
                return (FeatureObjectResultModel<RevokeApiKeyCommandResponse>.Error(new MessageItem
                {
                    Code = "ApiKey.NotFound",
                    Params = [cmd.ApiKeyId.ToString()]
                }), null);

            var revokedKeyHash = keyToRevoke.KeyValue.Hash;

            var result = merchant.RevokeApiKey(cmd.ApiKeyId);
            if (!result.IsSuccess)
                return (FeatureObjectResultModel<RevokeApiKeyCommandResponse>.Error(result.Messages!), null);

            var integrationEvent = new SharedMerchantEvents.ApiKeyRevoked(
                merchant.Id,
                revokedKeyHash,
                DateTime.UtcNow);

            return (FeatureObjectResultModel<RevokeApiKeyCommandResponse>.Ok(new RevokeApiKeyCommandResponse()), integrationEvent);
        }
    }
}