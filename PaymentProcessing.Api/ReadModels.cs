namespace PaymentProcessing.Api;

public class BankCommissionReadModel
{
    public Guid Id { get; private set; }
    public Guid BankId { get; private set; }
    public CardBrand CardBrand { get; private set; }
    public CardType CardType { get; private set; }
    public TransactionRegion TransactionRegion { get; private set; }
    public decimal Rate { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private BankCommissionReadModel() { }

    public static BankCommissionReadModel Upsert(BankCommissionUpdated evt) => new()
    {
        Id = evt.BankCommissionId,
        BankId = evt.BankId,
        CardBrand = evt.CardBrand,
        CardType = evt.CardType,
        TransactionRegion = evt.TransactionRegion,
        Rate = evt.Rate
    };
}

public class MerchantCommissionReadModel
{
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid BankCommissionId { get; private set; }
    public decimal Rate { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantCommissionReadModel() { }

    public static MerchantCommissionReadModel Upsert(MerchantCommissionUpdated evt) => new()
    {
        Id = evt.MerchantCommissionId,
        MerchantId = evt.MerchantId,
        BankCommissionId = evt.BankCommissionId,
        Rate = evt.Rate
    };
}