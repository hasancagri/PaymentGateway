using Marten;
using PaymentGateway.SharedContracts.MerchantEvents;

namespace PaymentProcessing.Api.Modules.PaymentProcessing.Merchants;

public static class MerchantEventHandlers
{
    public static async Task Handle(MerchantCreated evt, IDocumentSession session)
    {
        session.Store(new MerchantSummary
        {
            Id = evt.MerchantId,
            Name = evt.Name,
            Status = "Active"
        });
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantUpdated evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<MerchantSummary>(evt.MerchantId);
        if (summary is null) return;
        if (evt.Name is not null) summary.Name = evt.Name;
        if (evt.WebhookUrl is not null) summary.WebhookUrl = evt.WebhookUrl;
        session.Store(summary);
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantStatusChanged evt, IDocumentSession session)
    {
        var summary = await session.LoadAsync<MerchantSummary>(evt.MerchantId);
        if (summary is null) return;
        summary.Status = evt.NewStatus;
        session.Store(summary);
        await session.SaveChangesAsync();
    }
}