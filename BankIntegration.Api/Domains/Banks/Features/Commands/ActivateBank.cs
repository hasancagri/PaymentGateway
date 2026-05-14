using PaymentGatewayApi.Modules.BankIntegration.MerchantBanks.Enums;

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
            IMessageBus bus,
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

            var merchantBanks = await session.Query<MerchantBank>()
                .Where(mb => mb.BankId == cmd.BankId)
                .ToListAsync(ct);

            foreach (var mb in merchantBanks)
                await bus.PublishAsync(new MerchantBankSynced(
                    MerchantBankId: mb.Id,
                    MerchantId: mb.MerchantId,
                    BankId: mb.BankId,
                    BankName: bank.Name.Value,
                    IcaMemberId: bank.IcaMemberId,
                    SupportedCurrencies: bank.SupportedCurrencies,
                    IsActive: mb.Status == MerchantBankStatus.Active,
                    OccurredOn: DateTime.UtcNow));

            return FeatureObjectResultModel<ActivateBankCommandResponse>.Ok(new ActivateBankCommandResponse
            {
                BankId = cmd.BankId
            });
        }
    }
}