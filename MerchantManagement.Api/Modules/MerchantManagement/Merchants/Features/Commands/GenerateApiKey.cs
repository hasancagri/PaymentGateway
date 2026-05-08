using SharedMerchantEvents = PaymentGateway.SharedContracts.MerchantEvents;

namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Commands;

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
        public async Task<(FeatureObjectResultModel<GenerateApiKeyResponse>, SharedMerchantEvents.ApiKeyGenerated?)> Handle(
            GenerateApiKeyCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>()
                .Include(x => x.ApiKeys)
                .FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);

            if (merchant is null)
                return (FeatureObjectResultModel<GenerateApiKeyResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }), null);

            var result = merchant.GenerateApiKey();
            if (!result.IsSuccess)
                return (FeatureObjectResultModel<GenerateApiKeyResponse>.Error(result.Messages!), null);

            var keyValue = result.Data!;
            var integrationEvent = new SharedMerchantEvents.ApiKeyGenerated(
                merchant.Id,
                keyValue.Hash,
                DateTime.UtcNow);

            return (FeatureObjectResultModel<GenerateApiKeyResponse>.Ok(new GenerateApiKeyResponse { PlainTextKey = keyValue.PlainText }), integrationEvent);
        }
    }
}