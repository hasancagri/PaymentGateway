namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

public static class GenerateApiKey
{
    public class GenerateApiKeyCommand
    {
        public required Guid MerchantId { get; set; }
    }

    public class GenerateApiKeyResponse
    {
        public string PlainTextKey { get; set; }
    }

    [Transactional]
    public class GenerateApiKeyHandler
    {
        public async Task<(FeatureObjectResultModel<GenerateApiKeyResponse>, ApiKeyGenerated?)> Handle(
            GenerateApiKeyCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return (FeatureObjectResultModel<GenerateApiKeyResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }), null);

            var result = merchant.GenerateApiKey();
            if (!result.IsSuccess)
                return (FeatureObjectResultModel<GenerateApiKeyResponse>.Error(result.Messages!), null);

            session.Store(merchant);

            var keyValue = result.Data!;
            var integrationEvent = new ApiKeyGenerated(merchant.Id, keyValue.Hash, DateTime.UtcNow);

            return (FeatureObjectResultModel<GenerateApiKeyResponse>.Ok(new GenerateApiKeyResponse { PlainTextKey = keyValue.PlainText }), integrationEvent);
        }
    }
}