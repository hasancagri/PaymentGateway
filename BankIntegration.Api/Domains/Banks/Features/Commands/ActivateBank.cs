namespace BankIntegration.Api.Domains.Banks.Features.Commands;

public static class ActivateBank
{
    public class ActivateBankCommand
    {
        public required Guid BankId { get; set; }
    }

    public class ActivateBankCommandResponse
    {
        public Guid BankId { get; set; }
    }

    [Transactional]
    public class ActivateBankHandler
    {
        public async Task<FeatureObjectResultModel<ActivateBankCommandResponse>> Handle(
            ActivateBankCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var bank = await session.LoadAsync<Bank>(cmd.BankId, ct);
            if (bank is null)
                return FeatureObjectResultModel<ActivateBankCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Bank),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            bank.Activate();
            session.Store(bank);
            return FeatureObjectResultModel<ActivateBankCommandResponse>.Ok(new ActivateBankCommandResponse
            {
                BankId = cmd.BankId
            });
        }
    }
}