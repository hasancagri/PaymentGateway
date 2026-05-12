namespace PaymentProcessing.Api;

public static class EventHandlers
{
    public static async Task Handle(BankCommissionUpdated evt, IDocumentSession session)
    {
        session.Store(BankCommissionReadModel.Upsert(evt));
        await session.SaveChangesAsync();
    }

    public static async Task Handle(MerchantCommissionUpdated evt, IDocumentSession session)
    {
        session.Store(MerchantCommissionReadModel.Upsert(evt));
        await session.SaveChangesAsync();
    }
}