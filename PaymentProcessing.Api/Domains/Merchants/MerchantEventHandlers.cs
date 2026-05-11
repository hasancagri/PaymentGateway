using Marten;
using PaymentGateway.SharedContracts;

namespace PaymentProcessing.Api.PaymentProcessing.Merchants;

public static class MerchantEventHandlers
{
    public static async Task Handle(MerchantCreated evt, IDocumentSession session)
    {
        session.Store(MerchantSummary.Create(evt.MerchantId, evt.Name, evt.WebhookUrl));
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantUpdated evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<MerchantSummary>(evt.MerchantId);
        if (summary is null) return;
        summary.Update(evt.Name, evt.WebhookUrl);
        session.Store(summary);
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantStatusChanged evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<MerchantSummary>(evt.MerchantId);
        if (summary is null) return;
        summary.UpdateStatus(evt.NewStatus);
        session.Store(summary);
        await session.SaveChangesAsync();
    }
}