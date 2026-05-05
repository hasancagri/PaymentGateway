namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Commands;

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
        public async Task<FeatureObjectResultModel<GenerateApiKeyResponse>> Handle(
            GenerateApiKeyCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>()
                .Include(x => x.ApiKeys)
                .FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);

            if (merchant is null)
                return FeatureObjectResultModel<GenerateApiKeyResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.GenerateApiKey();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<GenerateApiKeyResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<GenerateApiKeyResponse>.Ok(new GenerateApiKeyResponse { PlainTextKey = result.Data!.PlainText });
        }
    }
}