namespace BankIntegration.Api.Domains.Banks.Features.Commands;

public static class AddBankCurrency
{
    public class AddBankCurrencyCommand
    {
        public required Guid BankId { get; set; }
        public required string CurrencyCode { get; set; }
    }
    
    public class AddBankCurrencyCommandResponse
    {
    }

    [Transactional]
    public class AddBankCurrencyHandler
    {
        public async Task<FeatureObjectResultModel<AddBankCurrencyCommandResponse>> Handle(
            AddBankCurrencyCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var bank = await session.LoadAsync<Bank>(cmd.BankId, ct);
            if (bank is null)
                return FeatureObjectResultModel<AddBankCurrencyCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Bank),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = bank.AddSupportedCurrency(cmd.CurrencyCode);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddBankCurrencyCommandResponse>.Error(result.Messages!);

            session.Store(bank);
            return FeatureObjectResultModel<AddBankCurrencyCommandResponse>.Ok(new AddBankCurrencyCommandResponse());
        }
    }
}