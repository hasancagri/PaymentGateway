using PaymentGatewayApi.Modules.MerchantManagement.Merchants.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.MerchantManagement.Merchants.Features.Commands;

public static class RemoveMerchantCurrency
{
    public class RemoveMerchantCurrencyCommand
    {
        public required Guid MerchantId { get; set; }
        public required string Currency { get; set; }
    }
    
    public class RemoveMerchantCurrencyCommandResponse
    {
    }

    [Transactional]
    public class RemoveMerchantCurrencyHandler
    {
        public async Task<FeatureObjectResultModel<RemoveMerchantCurrencyCommandResponse>> Handle(
            RemoveMerchantCurrencyCommand cmd,
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>()
                .Include(x => x.Currencies)
                .Include(x => x.BankAccounts)
                .FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);

            if (merchant is null)
                return FeatureObjectResultModel<RemoveMerchantCurrencyCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var currencyResult = Currency.Create(cmd.Currency);
            if (!currencyResult.IsSuccess)
                return FeatureObjectResultModel<RemoveMerchantCurrencyCommandResponse>.Error(currencyResult.Messages!);

            var result = merchant.RemoveCurrency(currencyResult.Data!);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RemoveMerchantCurrencyCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<RemoveMerchantCurrencyCommandResponse>.Ok(new RemoveMerchantCurrencyCommandResponse());
        }
    }
}