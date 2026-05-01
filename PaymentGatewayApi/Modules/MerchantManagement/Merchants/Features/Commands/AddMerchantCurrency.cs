using PaymentGatewayApi.Modules.MerchantManagement.Merchants.Entities;
using PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Commands;

public static class AddMerchantCurrency
{
    public class AddMerchantCurrencyCommand
    {
        public required Guid MerchantId { get; set; }
        public required string Currency { get; set; }
    }
    
    public class AddMerchantCurrencyCommandResponse
    {
    }

    [Transactional]
    public class AddMerchantCurrencyHandler
    {
        public async Task<FeatureObjectResultModel<AddMerchantCurrencyCommandResponse>> Handle(
            AddMerchantCurrencyCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>()
                .Include(x => x.Currencies)
                .FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);

            if (merchant is null)
                return FeatureObjectResultModel<AddMerchantCurrencyCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var currencyResult = Currency.Create(cmd.Currency);
            if (!currencyResult.IsSuccess)
                return FeatureObjectResultModel<AddMerchantCurrencyCommandResponse>.Error(currencyResult.Messages!);

            var result = merchant.AddCurrency(currencyResult.Data!);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddMerchantCurrencyCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<AddMerchantCurrencyCommandResponse>.Ok(new AddMerchantCurrencyCommandResponse());
        }
    }
}