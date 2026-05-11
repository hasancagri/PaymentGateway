namespace MerchantManagement.Api.Domains.Merchants.Features.Commands;

public static class RemoveMerchantBankAccount
{
    public class RemoveMerchantBankAccountCommand
    {
        public required Guid MerchantId { get; set; }
        public required Guid BankAccountId { get; set; }
    }

    public class RemoveMerchantBankAccountCommandResponse
    {
    }

    [Transactional]
    public class RemoveMerchantBankAccountHandler
    {
        public async Task<FeatureObjectResultModel<RemoveMerchantBankAccountCommandResponse>> Handle(
            RemoveMerchantBankAccountCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<RemoveMerchantBankAccountCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.RemoveBankAccount(cmd.BankAccountId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RemoveMerchantBankAccountCommandResponse>.Error(result.Messages!);

            session.Store(merchant);
            return FeatureObjectResultModel<RemoveMerchantBankAccountCommandResponse>.Ok(new RemoveMerchantBankAccountCommandResponse());
        }
    }
}