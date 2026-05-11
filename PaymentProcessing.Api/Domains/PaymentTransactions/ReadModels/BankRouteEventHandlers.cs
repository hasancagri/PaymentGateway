using Marten;
using PaymentGateway.SharedContracts.BankIntegrationEvents;
using PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

public static class BankRouteEventHandlers
{
    public static async Task Handle(BankRouteSynced evt, IDocumentSession session)
    {
        var existing = await session.Query<BankRouteSummary>()
            .Where(r => r.MerchantId == evt.MerchantId && r.Currency == evt.Currency)
            .FirstOrDefaultAsync(token: CancellationToken.None);

        var route = existing ?? BankRouteSummary.Create(Guid.NewGuid());
        route.Sync(evt.MerchantId, evt.BankId, evt.BankName, evt.Currency, evt.BankRate, evt.MerchantRate);

        session.Store(route);
        await session.SaveChangesAsync();
    }
}