using Marten;
using PaymentGateway.SharedContracts.BankIntegrationEvents;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ReadModels;

public static class BankRouteEventHandlers
{
    public static async Task Handle(BankRouteSynced evt, IDocumentSession session)
    {
        var existing = await session.Query<BankRouteSummary>()
            .Where(r => r.MerchantId == evt.MerchantId && r.Currency == evt.Currency)
            .FirstOrDefaultAsync(token: CancellationToken.None);

        var route = existing ?? new BankRouteSummary { Id = Guid.NewGuid() };
        route.MerchantId = evt.MerchantId;
        route.BankId = evt.BankId;
        route.BankName = evt.BankName;
        route.Currency = evt.Currency;
        route.BankRate = evt.BankRate;
        route.MerchantRate = evt.MerchantRate;

        session.Store(route);
        await session.SaveChangesAsync();
    }
}