using PaymentGateway.SyncContracts.BankIntegration;
using PaymentGateway.SyncContracts.Commission;
using PaymentGateway.SyncContracts.Merchant;

namespace PaymentProcessing.Api;

public class BankCommissionSummary
{
    public Guid Id { get; private set; }
    public Guid BankId { get; private set; }
    public CardBrand CardBrand { get; private set; }
    public CardType CardType { get; private set; }
    public TransactionRegion TransactionRegion { get; private set; }
    public decimal Rate { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private BankCommissionSummary() { }

    public static BankCommissionSummary From(BankCommissionSynced evt) => new()
    {
        Id = evt.BankCommissionId,
        BankId = evt.BankId,
        CardBrand = evt.CardBrand,
        CardType = evt.CardType,
        TransactionRegion = evt.TransactionRegion,
        Rate = evt.Rate
    };

    public static BankCommissionSummary From(BankCommissionItem item) => new()
    {
        Id = Guid.Parse(item.BankCommissionId),
        BankId = Guid.Parse(item.BankId),
        CardBrand = (CardBrand)item.CardBrand,
        CardType = (CardType)item.CardType,
        TransactionRegion = (TransactionRegion)item.TransactionRegion,
        Rate = decimal.Parse(item.Rate)
    };
}

public class MerchantCommissionSummary
{
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid BankCommissionId { get; private set; }
    public decimal Rate { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantCommissionSummary() { }

    public static MerchantCommissionSummary From(MerchantCommissionSynced evt) => new()
    {
        Id = evt.MerchantCommissionId,
        MerchantId = evt.MerchantId,
        BankCommissionId = evt.BankCommissionId,
        Rate = evt.Rate
    };

    public static MerchantCommissionSummary From(MerchantCommissionItem item) => new()
    {
        Id = Guid.Parse(item.MerchantCommissionId),
        MerchantId = Guid.Parse(item.MerchantId),
        BankCommissionId = Guid.Parse(item.BankCommissionId),
        Rate = decimal.Parse(item.Rate)
    };
}

public class MerchantBankSummary
{
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid BankId { get; private set; }
    public string BankName { get; private set; } = null!;
    public string? IcaMemberId { get; private set; }
    public IReadOnlyCollection<string> SupportedCurrencies { get; private set; } = [];
    public bool IsActive { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantBankSummary() { }

    public static MerchantBankSummary From(MerchantBankSynced evt) => new()
    {
        Id = evt.MerchantBankId,
        MerchantId = evt.MerchantId,
        BankId = evt.BankId,
        BankName = evt.BankName,
        IcaMemberId = evt.IcaMemberId,
        SupportedCurrencies = evt.SupportedCurrencies,
        IsActive = evt.IsActive
    };

    public static MerchantBankSummary From(MerchantBankItem item) => new()
    {
        Id = Guid.Parse(item.MerchantBankId),
        MerchantId = Guid.Parse(item.MerchantId),
        BankId = Guid.Parse(item.BankId),
        BankName = item.BankName,
        IcaMemberId = string.IsNullOrEmpty(item.IcaMemberId) ? null : item.IcaMemberId,
        SupportedCurrencies = item.SupportedCurrencies.ToList(),
        IsActive = item.IsActive
    };
}

public class MerchantSummary
{
    public Guid Id { get; private set; }
    public string WebhookUrl { get; private set; } = null!;
    public bool IsActive { get; private set; }

    [Newtonsoft.Json.JsonConstructor]
    private MerchantSummary() { }

    public static MerchantSummary From(MerchantSynced evt) => new()
    {
        Id = evt.MerchantId,
        WebhookUrl = evt.WebhookUrl,
        IsActive = evt.IsActive
    };

    public static MerchantSummary From(MerchantItem item) => new()
    {
        Id = Guid.Parse(item.MerchantId),
        WebhookUrl = item.WebhookUrl,
        IsActive = item.IsActive
    };
}