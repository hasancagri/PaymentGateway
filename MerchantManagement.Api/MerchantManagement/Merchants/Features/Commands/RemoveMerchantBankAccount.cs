namespace MerchantManagement.Api.Modules.MerchantManagement.Merchants.Features.Commands;

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
            MerchantManagementContext db,
            CancellationToken ct)
        {
            var merchant = await db.Set<Merchant>()
                .Include(x => x.BankAccounts)
                .FirstOrDefaultAsync(x => x.Id == cmd.MerchantId, ct);

            if (merchant is null)
                return FeatureObjectResultModel<RemoveMerchantBankAccountCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Merchant),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = merchant.RemoveBankAccount(cmd.BankAccountId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RemoveMerchantBankAccountCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<RemoveMerchantBankAccountCommandResponse>.Ok(new RemoveMerchantBankAccountCommandResponse());
        }
    }
}