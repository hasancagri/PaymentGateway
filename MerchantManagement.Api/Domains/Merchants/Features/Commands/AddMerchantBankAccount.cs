namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

public static class AddMerchantBankAccount
{
    public class AddMerchantBankAccountCommand
    {
        public required Guid MerchantId { get; set; }
        public required string Iban { get; set; }
        public required string SwiftCode { get; set; }
        public required string BankName { get; set; }
        public required string Currency { get; set; }
    }

    public class AddMerchantBankAccountCommandResponse
    {
    }

    [Transactional]
    public class AddMerchantBankAccountHandler
    {
        public async Task<FeatureObjectResultModel<AddMerchantBankAccountCommandResponse>> Handle(
            AddMerchantBankAccountCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<AddMerchantBankAccountCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var currencyResult = Currency.Create(cmd.Currency);
            if (!currencyResult.IsSuccess)
                return FeatureObjectResultModel<AddMerchantBankAccountCommandResponse>.Error(currencyResult.Messages!);

            var result = merchant.AddBankAccount(cmd.Iban, cmd.SwiftCode, cmd.BankName, currencyResult.Data!);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddMerchantBankAccountCommandResponse>.Error(result.Messages!);

            session.Store(merchant);
            return FeatureObjectResultModel<AddMerchantBankAccountCommandResponse>.Ok(new AddMerchantBankAccountCommandResponse());
        }
    }
}