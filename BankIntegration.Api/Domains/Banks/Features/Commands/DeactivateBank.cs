using Wolverine.Attributes;

namespace BankIntegration.Api.Domains.Banks.Features.Commands;

public static class DeactivateBank
{
    public class DeactivateBankCommand
    {
        public required Guid BankId { get; set; }
    }
    
    public class DeactivateBankCommandResponse
    {
    }

    [Transactional]
    public class DeactivateBankHandler
    {
        public async Task<FeatureObjectResultModel<DeactivateBankCommandResponse>> Handle(
            DeactivateBankCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var bank = await session.LoadAsync<Bank>(cmd.BankId, ct);
            if (bank is null)
                return FeatureObjectResultModel<DeactivateBankCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Bank),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            bank.Deactivate();
            session.Store(bank);
            return FeatureObjectResultModel<DeactivateBankCommandResponse>.Ok(new DeactivateBankCommandResponse());
        }
    }
}