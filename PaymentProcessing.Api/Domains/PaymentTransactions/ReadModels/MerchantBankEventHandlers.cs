using Marten;
using PaymentGateway.SharedContracts.BankIntegrationEvents;
using PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

public static class MerchantBankEventHandlers
{
    public static async Task Handle(MerchantBankSynced evt, IDocumentSession session)
    {
        var existing = await session.Query<MerchantBankSummary>()
            .Where(mb => mb.MerchantId == evt.MerchantId && mb.BankId == evt.BankId)
            .FirstOrDefaultAsync();

        var summary = existing ?? MerchantBankSummary.Create(Guid.NewGuid());
        summary.Sync(evt.MerchantId, evt.BankId, evt.BankName, evt.IcaMemberId,
            evt.SupportedCurrencies, evt.IsActive);

        session.Store(summary);
        await session.SaveChangesAsync();
    }
}