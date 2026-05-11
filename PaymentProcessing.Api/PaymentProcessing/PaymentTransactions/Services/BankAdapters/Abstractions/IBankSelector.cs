using Marten;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ReadModels;
using PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.ValueObjects;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Services.BankAdapters.Abstractions;

public interface IBankSelector : IScopedDependency
{
    Task<BankRoute> SelectBestAsync(Guid merchantId, string currency, CardProfile cardProfile, CancellationToken ct);
}

public class BankSelector(IDocumentSession session) : IBankSelector
{
    public async Task<BankRoute> SelectBestAsync(
        Guid merchantId, string currency, CardProfile cardProfile, CancellationToken ct)
    {
        var route = await Marten.QueryableExtensions.FirstOrDefaultAsync(
            session.Query<BankRouteSummary>()
                .Where(r => r.MerchantId == merchantId && r.Currency == currency),
            ct);

        if (route is null)
            throw new InvalidOperationException(
                $"No bank route found for merchant {merchantId}, currency {currency}.");

        return new BankRoute(route.BankId, route.BankName, route.BankRate, route.MerchantRate);
    }
}