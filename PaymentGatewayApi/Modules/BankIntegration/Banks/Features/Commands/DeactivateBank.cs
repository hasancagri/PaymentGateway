using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.BankIntegration.Banks.Features.Commands;

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
            BankIntegrationContext db,
            CancellationToken ct)
        {
            var bank = await db.Set<Bank>().FirstOrDefaultAsync(x => x.Id == cmd.BankId, ct);
            if (bank is null)
                return FeatureObjectResultModel<DeactivateBankCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Bank),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            bank.Deactivate();
            return FeatureObjectResultModel<DeactivateBankCommandResponse>.Ok(new DeactivateBankCommandResponse());
        }
    }
}