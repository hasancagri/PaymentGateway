namespace BankIntegration.Api.Domains.MerchantBanks.Features.Commands;

public static class AssignMerchantBank
{
    public class AssignMerchantBankCommand
    {
        public required Guid BankId { get; set; }
        public required Guid MerchantId { get; set; }
        public required string MerchantCode { get; set; }
        public required string TerminalCode { get; set; }
    }

    public class AssignMerchantBankResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AssignMerchantBankHandler
    {
        public async Task<FeatureObjectResultModel<AssignMerchantBankResponse>> Handle(
            AssignMerchantBankCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var bank = await session.LoadAsync<Bank>(cmd.BankId, ct);
            if (bank is null)
                return FeatureObjectResultModel<AssignMerchantBankResponse>.Error(new MessageItem
                {
                    Table = nameof(Bank),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var result = MerchantBank.Assign(cmd.MerchantId, cmd.BankId, cmd.MerchantCode, cmd.TerminalCode);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AssignMerchantBankResponse>.Error(result.Messages!);

            var merchantBank = result.Data!;
            session.Store(merchantBank);

            await bus.PublishAsync(new MerchantBankSynced(
                MerchantBankId: merchantBank.Id,
                MerchantId: merchantBank.MerchantId,
                BankId: merchantBank.BankId,
                BankName: bank.Name.Value,
                IcaMemberId: bank.IcaMemberId,
                SupportedCurrencies: bank.SupportedCurrencies,
                IsActive: true,
                OccurredOn: DateTime.UtcNow));

            return FeatureObjectResultModel<AssignMerchantBankResponse>.Ok(
                new AssignMerchantBankResponse { Id = merchantBank.Id });
        }
    }
}