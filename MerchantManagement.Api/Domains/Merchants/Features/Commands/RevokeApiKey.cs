namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

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
        public async Task<(FeatureObjectResultModel<RevokeApiKeyCommandResponse>, ApiKeyRevoked?)> Handle(
            RevokeApiKeyCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
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

            session.Store(merchant);

            var integrationEvent = new ApiKeyRevoked(merchant.Id, revokedKeyHash, DateTime.UtcNow);
            return (FeatureObjectResultModel<RevokeApiKeyCommandResponse>.Ok(new RevokeApiKeyCommandResponse()), integrationEvent);
        }
    }
}