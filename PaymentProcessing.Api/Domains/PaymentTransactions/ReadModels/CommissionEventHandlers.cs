using Marten;
using PaymentGateway.SharedContracts.CommissionEvents;
using PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.ReadModels;

public static class CommissionEventHandlers
{
    public static async Task Handle(BankCommissionSynced evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<BankCommissionSummary>(evt.BankCommissionId)
                      ?? BankCommissionSummary.Create(evt.BankCommissionId);

        summary.Sync(evt.BankId, evt.CardBrand, evt.CardType, evt.TransactionRegion, evt.Rate);

        session.Store(summary);
        await session.SaveChangesAsync();
    }

    public static async Task Handle(BankCommissionRateUpdated evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<BankCommissionSummary>(evt.BankCommissionId);
        if (summary is null) return;

        summary.UpdateRate(evt.NewRate);
        session.Store(summary);
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantCommissionSynced evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<MerchantCommissionSummary>(evt.MerchantCommissionId)
                      ?? MerchantCommissionSummary.Create(evt.MerchantCommissionId);

        summary.Sync(evt.MerchantId, evt.BankCommissionId, evt.Rate);

        session.Store(summary);
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantCommissionRateUpdated evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<MerchantCommissionSummary>(evt.MerchantCommissionId);
        if (summary is null) return;

        summary.UpdateRate(evt.NewRate);
        session.Store(summary);
        await session.SaveChangesAsync();
    }
}