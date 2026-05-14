namespace PaymentProcessing.Api;

public static class EventHandlers
{
    public static async Task Handle(BankCommissionSynced evt, IDocumentSession session)
    {
        session.Store(BankCommissionSummary.From(evt));
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantCommissionSynced evt, IDocumentSession session)
    {
        session.Store(MerchantCommissionSummary.From(evt));
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantBankSynced evt, IDocumentSession session)
    {
        session.Store(MerchantBankSummary.From(evt));
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantSynced evt, IDocumentSession session)
    {
        session.Store(MerchantSummary.From(evt));
        await session.SaveChangesAsync();
    }
}