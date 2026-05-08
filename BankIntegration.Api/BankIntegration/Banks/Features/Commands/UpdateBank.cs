namespace PaymentGatewayApi.Modules.BankIntegration.Banks.Features.Commands;

public static class UpdateBank
{
    public class UpdateBankCommand
    {
        public required Guid BankId { get; set; }
        public required string Name { get; set; }
        public required int Priority { get; set; }
        public required string ApiUrl { get; set; }
        public string? IcaMemberId { get; set; }
    }
    
    public class UpdateBankCommandResponse
    {
    }

    [Transactional]
    public class UpdateBankHandler
    {
        public async Task<FeatureObjectResultModel<UpdateBankCommandResponse>> Handle(
            UpdateBankCommand cmd,
            BankIntegrationContext db,
            CancellationToken ct)
        {
            var bank = await db.Set<Bank>().FirstOrDefaultAsync(x => x.Id == cmd.BankId, ct);
            if (bank is null)
                return FeatureObjectResultModel<UpdateBankCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(Bank),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = bank.Update(cmd.Name, cmd.Priority, cmd.ApiUrl, cmd.IcaMemberId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateBankCommandResponse>.Error(result.Messages!);

            return FeatureObjectResultModel<UpdateBankCommandResponse>.Ok(new UpdateBankCommandResponse());
        }
    }
}